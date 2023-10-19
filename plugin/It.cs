using System.Collections.Generic;
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

        public static void Init(RWMod mod)
        {
            logger = mod.logSource;

            /*IL.Player.Update += (il) =>
            {
                logger.LogMessage("IL Hooking Player.Update...");

                try {
                    ILCursor c = new ILCursor(il);
                    ILProcessor proc = c.Body.GetILProcessor();
                    
                    Instruction noOverride = il.Body.Instructions[0];

                    c.Goto(0); // insert at the start of function
                    c.Emit(OpCodes.Ldarg_0); // load this
                    c.EmitDelegate<Func<Player, bool>>(Player_Update); // call It update method
                    c.Emit(OpCodes.Brfalse, noOverride); // if update method did anything
                        c.Emit(OpCodes.Ldarg_0); // load this
                        c.Emit(OpCodes.Ldarg_1); // load eu parameter
                        c.Emit(OpCodes.Call, typeof(Creature).GetMethod("Update")); // call Creature::Update 
                        c.Emit(OpCodes.Ret); // exit function early
                }
                catch (Exception e)
                {
                    logger.LogError(e);
                }
            };*/
        }

        public static void ApplyHooks()
        {
            On.PlayerGraphics.DefaultFaceSprite += PlayerGraphics_DefaultFaceSprite;
            On.PlayerGraphics.DrawSprites += PlayerGraphics_DrawSprites;
            On.Player.NewRoom += Player_NewRoom;
            On.Room.Update += Room_Update;
            On.RoomCamera.Update += RoomCamera_Update;
            On.Player.ShortCutColor += Player_ShortCutColor;
            On.Player.Update += Player_Update;
        }

        public static void Cleanup()
        {
            ghosts.Clear();
        }

        public static void Reset()
        {
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

                    // start spawn timer if this room isn't a shelter
                    if (!newRoom.abstractRoom.shelter)
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

        // Spawn "it" in a story session
        private static void SpawnIt(Room realRoom)
        {
            AbstractRoom room = realRoom.abstractRoom;
            if (room.shelter) return;
            if (!realRoom.game.IsStorySession) return;

            var playerState = realRoom.game.session.Players[0].state as PlayerState;
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

            WorldCoordinate leading = realRoom.world.NodeInALeadingToB(exitRoom, room.index);

            newCreature.ChangeRooms(
                new WorldCoordinate(room.index, 0, 0, leading.abstractNode)
            );

            Debug.Log("Spawned IT");
        }

        private static void Room_Update(On.Room.orig_Update orig, Room self)
        {
            orig(self);

            Room activeRoom = self.game.cameras[0].room;
            if (self != activeRoom) return;
            //idk why this is broken ):
            bool pressState = Input.GetKey(KeyCode.G);

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

        private static string PlayerGraphics_DefaultFaceSprite(On.PlayerGraphics.orig_DefaultFaceSprite orig, PlayerGraphics self, float eyeScale)
        {
            if (ghosts.Contains(self.player.abstractCreature))
            {
                return "FaceE";
            }

            return orig(self, eyeScale);
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
            }
        }

        // darken screen based off distance from "It" to target player
        // the amount to darken screen by is determined in Player_Update
        private static void RoomCamera_Update(On.RoomCamera.orig_Update orig, RoomCamera self)
        {
            orig(self);
            self.effect_darkness = 1f - (1f - self.effect_darkness) * darknessMultiplier;
        }

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

        private static void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (ghosts.Contains(self.abstractCreature))
            {
                darknessMultiplier = 1f;
                self.room.PlaySound(SoundID.Death_Rain_LOOP, self.mainBodyChunk);
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
                    self.bodyChunks[0].pos += delta;
                    self.bodyChunks[1].pos = self.bodyChunks[0].pos + self.bodyChunkConnections[0].distance * Vector2.down;

                    // if i touch player, freeze the application :trolle:
                    float dist = (targetPos.Value - selfPos).magnitude;
                    if (dist < 30f && !self.abstractCreature.world.game.devToolsActive)
                    {
                        targetPlayer.Die();

                        // only if this room is being viewed
                        if (isRoomViewed)
                            while (true) {}
                    }

                    // make screen darker the closer it is to the player
                    if (isRoomViewed)
                    {
                        darknessMultiplier = Mathf.Clamp01((dist - 90) / (600 - 90));
                    }
                }
            }
            else
            {
                orig(self, eu);
            }
        }
    }
}