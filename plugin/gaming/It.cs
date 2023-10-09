using BepInEx;
using DevInterface;
using UnityEngine;

namespace RWMod
{
    public partial class RWMod : BaseUnityPlugin
    {
        private bool oldPressState = false;
        private int spawnTicker = -1;

        private void Player_NewRoom(On.Player.orig_NewRoom orig, Player self, Room newRoom)
        {
            orig(self, newRoom);

            if (self.abstractCreature == self.room.game.cameras[0].followAbstractCreature)
            {
                Debug.Log("New Room");

                foreach (AbstractWorldEntity ghost in ghosts)
                {
                    ghost.Room.realizedRoom.RemoveObject((ghost as AbstractCreature).realizedCreature);
                    ghost.Room.RemoveEntity(ghost);
                }

                ghosts.Clear();

                // start spawn timer if this room isn't a shelter
                if (!newRoom.abstractRoom.shelter)
                {
                    if (Random.value < Options.SpawnChance.Value / 100f)
                    {
                        Debug.Log("Begin Spawn IT");
                        spawnTicker = 200;
                    }
                }
            }
        }

        // Spawn "it" in a story session
        private void SpawnIt(Room realRoom)
        {
            AbstractRoom room = realRoom.abstractRoom;
            if (room.shelter) return;
            if (!realRoom.game.IsStorySession) return;

            var playerState = realRoom.game.session.Players[0].state as PlayerState;
            var entityID = realRoom.game.GetNewID();

            Debug.Log("player pos: " + playerState.creature.pos.abstractNode);

            WorldCoordinate coords;
            int exitRoom = -1;
            int exitIndex;

            exitIndex = Random.Range(0, room.connections.Length);
            exitRoom = room.connections[exitIndex];
            coords = new WorldCoordinate(exitRoom, 0, 0, 0);
            
            var template = StaticWorld.GetCreatureTemplate(MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC);
            var newCreature = new AbstractCreature(realRoom.world, template, null, coords, entityID);
            newCreature.state = new MoreSlugcats.PlayerNPCState(newCreature, 0);
            ghosts.Add(newCreature);

            isShiny[newCreature.ID] = true;

            WorldCoordinate leading = realRoom.world.NodeInALeadingToB(exitRoom, room.index);

            newCreature.ChangeRooms(
                new WorldCoordinate(room.index, 0, 0, leading.abstractNode)
            );

            Debug.Log("Spawned IT");
        }

        private void Room_Update(On.Room.orig_Update orig, Room self)
        {
            orig(self);

            Room activeRoom = self.game.cameras[0].room;
            if (self != activeRoom) return;

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

        private string PlayerGraphics_DefaultFaceSprite(On.PlayerGraphics.orig_DefaultFaceSprite orig, PlayerGraphics self, float eyeScale)
        {
            if (isShiny.TryGetValue(self.player.abstractCreature.ID, out bool creatureIsShiny) && creatureIsShiny)
            {
                return "FaceE";
            }

            return orig(self, eyeScale);
        }

        private void PlayerGraphics_DrawSprites(
            On.PlayerGraphics.orig_DrawSprites orig,
            PlayerGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            float timeStacker,
            Vector2 camPos
        )
        {
            orig(self, sLeaser, rCam, timeStacker, camPos);

            if (IsCreatureShiny(self.player.abstractCreature))
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

        // "It" always emits a sound
        private void AbstractCreature_Realize(On.AbstractCreature.orig_Realize orig, AbstractCreature self)
        {
            orig(self);

            if (self.realizedCreature.Template.type == MoreSlugcats.MoreSlugcatsEnums.CreatureTemplateType.SlugNPC && IsCreatureShiny(self))
            {
                Debug.Log("begin sound pls...");
            }
        }

        private void Player_Update(On.Player.orig_Update orig, Player self, bool eu)
        {
            if (IsCreatureShiny(self.abstractCreature))
            {
                ChunkSoundEmitter emitter = self.room.PlaySound(SoundID.Death_Rain_LOOP, self.mainBodyChunk);

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
                        if (player.room.abstractRoom != self.room.abstractRoom || player.dead) continue;

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

                    // if i touch player, force quit the application :trolle:
                    if ((targetPos.Value - selfPos).magnitude < 30f && !self.abstractCreature.world.game.devToolsActive)
                    {
                        targetPlayer.Die();

                        // if this room is being viewed, actually just quit to menu
                        if (self.abstractCreature.world.game.cameras[0].room == self.room)
                        {
                            self.abstractCreature.world.game.ExitToMenu();
                        }
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