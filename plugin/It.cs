using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RWMod
{
    public class It
    {
        private static readonly List<AbstractWorldEntity> ghosts = new List<AbstractWorldEntity>();
        
        private static bool oldPressState = false;
        private static int spawnTicker = -1;
        private static AbstractRoom previousRoom = null;
        private static float darknessMultiplier = 1f;

        private static BepInEx.Logging.ManualLogSource logger;
        private static bool gameCrash = false;

        public static void Init(RWMod mod)
        {
            logger = mod.logSource;
        }

        public static void ApplyHooks()
        {
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.PlayerGraphics.Update += PlayerGraphics_Update;
            On.Player.NewRoom += Player_NewRoom;
            On.Room.Update += Room_Update;
            On.RoomCamera.Update += RoomCamera_Update;
            On.Player.ShortCutColor += Player_ShortCutColor;
            On.Player.Update += Player_Update;

            // always able to hit IT
            On.Weapon.HitThisObject += (On.Weapon.orig_HitThisObject orig, Weapon self, PhysicalObject obj) =>
            {
                if (obj is Player player && ghosts.Contains(player.abstractCreature))
                    return true;

                return orig(self, obj);
            };

            // create singularity bomb explosion
            // when IT dies
            On.Player.Die += (On.Player.orig_Die orig, Player self) =>
            {
                Room room = self.room ?? self.abstractCreature.world.GetAbstractRoom(self.abstractCreature.pos).realizedRoom;
                bool wasAlive = !self.dead;
                orig(self);

                if (wasAlive && ghosts.Contains(self.abstractCreature) && room != null)
                {
                    ghosts.Remove(self.abstractCreature);

                    AbstractPhysicalObject abstractBomb = new(
                        room.world,
                        MoreSlugcats.MoreSlugcatsEnums.AbstractObjectType.SingularityBomb,
                        null,
                        room.GetWorldCoordinate(self.mainBodyChunk.pos),
                        room.world.game.GetNewID()
                    );
                    room.abstractRoom.AddEntity(abstractBomb);
                    abstractBomb.RealizeInRoom();
                    (abstractBomb.realizedObject as MoreSlugcats.SingularityBomb).Explode();
                    abstractBomb.realizedObject.Destroy();

                    darknessMultiplier = 1f;
                    self.Destroy();
                }
            };

            // lizards fear It
            On.LizardAI.IUseARelationshipTracker_UpdateDynamicRelationship += (
                On.LizardAI.orig_IUseARelationshipTracker_UpdateDynamicRelationship orig,
                LizardAI self,
                RelationshipTracker.DynamicRelationship dRelation
            ) =>
            {
                AbstractCreature representedCreature = dRelation.trackerRep.representedCreature;

                if (
                    representedCreature.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC &&
                    ghosts.Contains(representedCreature)
                )
                {
                    return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Afraid, 1200f);
                }

                return orig(self, dRelation);
            };

            // scavengers also fear It
            On.ScavengerAI.IUseARelationshipTracker_UpdateDynamicRelationship += (
                On.ScavengerAI.orig_IUseARelationshipTracker_UpdateDynamicRelationship orig,
                ScavengerAI self,
                RelationshipTracker.DynamicRelationship dRelation
            ) =>
            {
                AbstractCreature representedCreature = dRelation.trackerRep.representedCreature;

                if (
                    representedCreature.creatureTemplate.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC &&
                    ghosts.Contains(representedCreature)
                )
                {
                    return new CreatureTemplate.Relationship(CreatureTemplate.Relationship.Type.Afraid, 1200f);
                }

                return orig(self, dRelation);
            };
        }

        public static void Cleanup()
        {
            ghosts.Clear();
            gameCrash = false;
        }

        public static void Reset()
        {
            gameCrash = false;
            spawnTicker = -1;
            previousRoom = null;
            darknessMultiplier = 1f;
        }

        private static void Player_NewRoom(On.Player.orig_NewRoom orig, Player self, Room newRoom)
        {
            orig(self, newRoom);

            if (self.abstractCreature == self.room.game.cameras[0].followAbstractCreature)
            {
                if (newRoom.abstractRoom != previousRoom)
                {
                    previousRoom = newRoom.abstractRoom;

                    Debug.Log("New Room");

                    foreach (AbstractCreature ghost in ghosts)
                    {
                        if (ghost.realizedCreature != null)
                        {
                            ghost.Room.realizedRoom.RemoveObject(ghost.realizedCreature);
                        }

                        ghost.Room.RemoveEntity(ghost);
                    }

                    ghosts.Clear();

                    // start spawn timer if this room isn't a shelter/gate
                    if (ItCanSpawn(newRoom))
                    {
                        float diceRoll = Random.value;
                        
                        Debug.Log($"IT Spawn: {(int)(diceRoll * 100f)} < {Options.SpawnChance.Value}");

                        if (diceRoll < Options.SpawnChance.Value / 100f)
                        {
                            Debug.Log("Success!");
                            spawnTicker = 200;
                            newRoom.PlaySound(SoundID.SS_AI_Give_The_Mark_Boom, self.mainBodyChunk);
                        }
                    }
                }
            }
        }

        private static readonly string[] prohibitedRooms = new string[] {
            "SS_AI",    // Five Pebbles' room
            "SL_AI",    // LTTM's room
            "DM_AI",    // LTTM's room in Spearmaster
            "LC_FINAL", // Artificer boss fight room
            "SB_L01",   // Void Sea room
            "MS_CORE",  // LTTM's core in Rivulet
            "RM_CORE",  // Five Pebbles' core in Rivulet
        };

        private static bool ItCanSpawn(Room room)
        {
            if (!room.game.IsStorySession) return false;

            AbstractRoom absRoom = room.abstractRoom;

            // make it unable to spawn in rooms that the player is
            // forced to stay in for a prolonged period of time
            // (i.e., shelters, gates, iterator rooms, e.t.c.)
            if (absRoom.shelter || absRoom.gate) return false;
            if (prohibitedRooms.Contains(absRoom.name))
                return false;

            return true;
        }

        // Spawn "it" in a story session
        private static void SpawnIt(Room realRoom)
        {
            if (!ItCanSpawn(realRoom)) return;
            AbstractRoom room = realRoom.abstractRoom;

            var entityID = realRoom.game.GetNewID();
            
            WorldCoordinate coords;
            int exitRoom;
            int exitIndex;

            exitIndex = Random.Range(0, room.connections.Length);
            exitRoom = room.connections[exitIndex];
            coords = new WorldCoordinate(exitRoom, 0, 0, 0);
            
            var template = StaticWorld.GetCreatureTemplate(MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC);
            var newCreature = new AbstractCreature(realRoom.world, template, null, coords, entityID);
            newCreature.state = new MoreSlugcats.PlayerNPCState(newCreature, 0);
            ghosts.Add(newCreature);

            newCreature.ChangeRooms(
                new WorldCoordinate(room.index, 0, 0, exitIndex)
            );

            Debug.Log("Spawned IT");
        }

        private static void Room_Update(On.Room.orig_Update orig, Room self)
        {
            orig(self);

            Room activeRoom = self.game.cameras[0].room;
            if (self != activeRoom) return;
            //idk why this is broken ):
            bool pressState = Input.GetKey(KeyCode.Keypad5);

            if (self.game.IsStorySession && ModManager.MSC)
            {
                if ((self.game.devToolsActive && pressState != oldPressState && pressState) || spawnTicker == 0)
                {
                    Debug.Log("Attempt to spawn IT");
                    SpawnIt(activeRoom);
                }
            }

            if (spawnTicker >= 0)
            {
                spawnTicker--;
            }

            oldPressState = pressState;
        }

        private static void PlayerGraphics_DrawSprites(
            On.PlayerGraphics.orig_DrawSprites orig,
            PlayerGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            float timeStacker,
            Vector2 camPos
        )
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (ghosts.Contains(self.player.abstractCreature))
            {
                Color color = new Color(0.01f, 0.01f, 0.01f);

                for (int i = 0; i < sLeaser.sprites.Length; i++)
                {
                    if (i != 9)
                    {
                        sLeaser.sprites[i].color = color;
                    }

                }

                sLeaser.sprites[9].color = new Color(1f, 0f, 0f);
                sLeaser.sprites[9].element = Futile.atlasManager.GetElementWithName("FaceE0");
            }
        }

        private static void PlayerGraphics_Update(On.PlayerGraphics.orig_Update orig, PlayerGraphics self)
        {
            orig(self);

            // make sure that It doesn't look at anything
            // it makes it seem more slugcat-y, which is bad for something
            // that's supposed to be scary
            if (ghosts.Contains(self.player.abstractCreature))
            {
                self.objectLooker.LookAtNothing();
            }
        }

        // darken screen based off distance from "It" to target player
        // the amount to darken screen by is determined in Player_Update
        private static void RoomCamera_Update(On.RoomCamera.orig_Update orig, RoomCamera self)
        {
            orig(self);
            self.effect_darkness = 1f - (1f - self.effect_darkness) * darknessMultiplier;
        }

        // set shortcut color of It to red
        private static Color Player_ShortCutColor(On.Player.orig_ShortCutColor orig, Player self)
        {
            if (ghosts.Contains(self.abstractCreature))
            {
                return new Color(1f, 0f, 0f);
            }
            else
            {
                return orig(self);
            }
        }

        // "IT" update procedure
        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            // when the player is caught, this will freeze the game
            // but also allow the player to exit to the menu
            if (gameCrash)
            {
                throw new System.Exception("CAUGHT YOU");
            }

            if (ghosts.Contains(self.abstractCreature))
            {
                darknessMultiplier = 1f;
                
                if (self.Consious) self.bodyMode = Player.BodyModeIndex.Stand;
                if (!self.dead) self.room.PlaySound(
                    SoundID.Death_Rain_LOOP,
                    self.mainBodyChunk,
                    false,
                    Options.ItVolume.Value / 100f,
                    1.0f
                );
                bool isRoomViewed = self.abstractCreature.world.game.cameras[0].room == self.room;

                // get nearest player
                Vector2? targetPos = null;
                Player targetPlayer = null;
                float minDist = float.PositiveInfinity;
                Vector2 selfPos = self.bodyChunks[0].pos;

                foreach (AbstractCreature abstractCreature in self.abstractCreature.world.game.session.Players)
                {
                    if (abstractCreature.realizedCreature != null)
                    {
                        Player player = abstractCreature.realizedCreature as Player;
                        if (player.dead || (player.room != null && player.room.abstractRoom != self.room.abstractRoom)) continue;
                        
                        Vector2 pos = player.bodyChunks[0].pos;

                        if ((pos - selfPos).magnitude < minDist)
                        {
                            minDist = (pos - selfPos).magnitude;
                            targetPos = pos;
                            targetPlayer = player;
                        }
                    }
                }

                // glide to nearest player
                if (targetPos != null && targetPlayer != null)
                {
                    Vector2 delta = (targetPos.Value - selfPos).normalized * 2f;
                    float dist = (targetPos.Value - selfPos).magnitude;

                    // glide to nearest player if Consious
                    if (self.Consious)
                    {
                        self.bodyChunks[0].pos += delta;
                        self.bodyChunks[1].pos = self.bodyChunks[0].pos + self.bodyChunkConnections[0].distance * Vector2.down;
                        self.bodyChunks[0].vel = Vector2.zero;
                        self.bodyChunks[1].vel = Vector2.zero;

                        // if i touch player, freeze the application :trolle:
                        if (!targetPlayer.inShortcut && dist < 30f && !self.abstractCreature.world.game.devToolsActive)
                        {
                            // only if this room is being viewed
                            if (isRoomViewed)
                                gameCrash = true;
                            else
                                targetPlayer.Die();
                        }
                    }

                    // make screen darker the closer it is to the player
                    if (isRoomViewed && !self.dead)
                    {
                        darknessMultiplier = Mathf.Clamp01((dist - 90) / (600 - 90));
                    }
                }

                // if not Consious, will be affected by physics
                if (!self.Consious)
                    orig(self, eu);
            }
            else
            {
                orig(self, eu);
            }
        }
    }
}