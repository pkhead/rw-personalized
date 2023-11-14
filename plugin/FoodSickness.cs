using System;
using System.Collections.Generic;
using UnityEngine;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System.Text.RegularExpressions;

namespace RWMod
{
    public class FoodSickness
    {
        private static RWMod mod;
        private readonly static Dictionary<AbstractPhysicalObject, int> sicknessData = new();
        private readonly static Dictionary<AbstractPhysicalObject, int> delayedSicknessData = new();

        private class PlayerData
        {
            // if the player is force-vomiting
            public bool isVomiting;

            public PlayerData()
            {
                isVomiting = false;
            }
        };

        private readonly static List<PlayerData> playerData = new();

        // used for game serialization
        private readonly static List<int> sicknessSaveData = new();

        public static void Init(RWMod mod)
        {
            FoodSickness.mod = mod;

            // color player as if they were malnourished
            // if player has food poisoning
            IL.PlayerGraphics.ApplyPalette += (il) =>
            {
                mod.logSource.LogDebug("PlayerGraphics.ApplyPalette IL injection...");

                try
                {
                    ILCursor cursor = new(il);

                    static bool isSickFunc(PlayerGraphics self) => SicknessLevel(self.player.abstractCreature) > 0;

                    // if (this.malnourished > 0f)
                    cursor.GotoNext(
                        x => x.MatchLdarg(0),
                        x => x.MatchLdfld<PlayerGraphics>("malnourished"),
                        x => x.MatchLdcR4(0.0f),
                        x => x.MatchBleUn(out _)
                    );
                    var branch = cursor.Body.Instructions[cursor.Index + 4];

                    // if (SicknessLevel(self.player.abstractCreature) > 1 || this.malnourished > 0f)
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate(isSickFunc);
                    cursor.Emit(OpCodes.Brtrue, branch);

                    // color2 = Color.Lerp(color2, Color.gray, 0.4f * num);
                    cursor.GotoNext(
                        x => x.MatchLdloc(1),
                        x => x.MatchCall<Color>("get_gray"),
                        x => x.MatchLdcR4(out _),
                        x => x.MatchLdloc(2),
                        x => x.MatchMul(),
                        x => x.MatchCall(out _), // assume Color.Lerp
                        x => x.MatchStloc(1)
                    );

                    ILLabel label = cursor.DefineLabel();

                    // if (SicknessLevel(this.player.abstractCreature) > 1) {
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate(isSickFunc);
                    cursor.Emit(OpCodes.Brfalse, label);

                    // num = 1f;
                    cursor.Emit(OpCodes.Ldc_R4, 1f);
                    cursor.Emit(OpCodes.Stloc_2);

                    // }
                    cursor.MarkLabel(label);

                    mod.logSource.LogDebug("IL injection success!");
                }
                catch (Exception e)
                {
                    mod.logSource.LogError(e);
                }
            };

            // force vomiting
            IL.Player.GrabUpdate += (il) =>
            {
                mod.logSource.LogDebug("Player.GrabUpdate IL injection...");

                try
                {
                    ILCursor cursor = new(il);

                    static bool isVomiting(Player self) {
                        return GetPlayerData(self).isVomiting;
                    };
                    
                    // if (flag3)
                    /*cursor.GotoNext(
                        x => x.MatchLdloc(1),
                        x => x.MatchBrfalse(out _)
                    );*/
                    ILLabel branch = null;

                    // if (ModManager.MMF && base.mainBodyChunk.submersion >= 0.5f)
                    cursor.GotoNext(
                        x => x.MatchLdsfld(typeof(ModManager).GetField("MMF")),
                        x => x.MatchBrfalse(out branch),
                        x => x.MatchLdarg(0),
                        x => x.MatchCall(typeof(Creature).GetMethod("get_mainBodyChunk")),
                        x => x.MatchCallvirt(typeof(BodyChunk).GetMethod("get_submersion")),
                        x => x.MatchLdcR4(out _),
                        x => x.MatchBltUn(out _)
                    );

                    if (branch == null)
                        throw new Exception("GotoNext search failed");
                    cursor.GotoLabel(branch);

                    ILLabel label = cursor.DefineLabel();

                    /*
                    if (GetPlayerData(self).isVomiting)
                        flag3 = true;
                    */
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate(isVomiting);
                    cursor.Emit(OpCodes.Brfalse, label);
                    cursor.Emit(OpCodes.Ldc_I4_1);
                    cursor.Emit(OpCodes.Stloc_1);
                    cursor.MarkLabel(label);

                    // else if (!ModManager.MMF || this.input[0].y == 0)
                    ILLabel vomitBranch = null;

                    cursor.GotoNext(
                        x => x.MatchLdsfld(typeof(ModManager).GetField("MMF")),
                        x => x.MatchBrfalse(out vomitBranch),
                        x => x.MatchLdarg(0),
                        x => x.MatchCall(out _), // assume Player/InputPackage[] Player::get_input()
                        x => x.MatchLdcI4(0),
                        x => x.MatchLdelema(out _), // assume Player/InputPackage
                        x => x.MatchLdfld(out _), // assume Player/InputPackage::y
                        x => x.MatchBrtrue(out _)
                    );

                    if (vomitBranch == null)
                        throw new Exception("GotoNext search failed");
                    
                    cursor.Emit(OpCodes.Ldarg_0);
                    cursor.EmitDelegate(isVomiting);
                    cursor.Emit(OpCodes.Brtrue, vomitBranch);

                    cursor.GotoLabel(vomitBranch);
                    cursor.EmitDelegate(() => Debug.Log("pls vomit"));

                    mod.logSource.LogDebug("IL injection success!");
                    mod.logSource.LogDebug(il.ToString());
                }
                catch (Exception e)
                {
                    mod.logSource.LogError(e);
                }
            };
        }

        public static void Cleanup()
        {
            sicknessData.Clear();
            delayedSicknessData.Clear();
            playerData.Clear();
        }

        private static PlayerData GetPlayerData(Player player)
        {
            int index = player.abstractCreature.world.game.Players.IndexOf(player.abstractCreature);
            return playerData[index];
        }
        
        public static void Infect(AbstractPhysicalObject thing)
        {
            // if creature was not already sick
            if (DelayedSicknessLevel(thing) == 0)
            {
                Debug.Log("infect creature");

                // one cycle of food poisoning = 2 levels
                delayedSicknessData.Add(thing, 2);
            }
            else
            {
                // if creature decides to eat 2 rotten fruit,
                // then they will be sick for an extra cycle
                // 2 / 2 = 1
                delayedSicknessData[thing] += 1;
            }
        }

        // the poisoned level a creature has on this cycle
        public static int SicknessLevel(AbstractPhysicalObject thing)
        {
            if (sicknessData.TryGetValue(thing, out var value))
                return value;

            return 0;
        }

        // the poisoned level a creature will have on the next cycle
        public static int DelayedSicknessLevel(AbstractPhysicalObject thing)
        {
            if (delayedSicknessData.TryGetValue(thing, out var value))
                return value;

            return 0;
        }

        #region Hooks

        public static void ApplyHooks()
        {
            // player moves slower when they have food poisoning
            On.Player.ctor += (On.Player.orig_ctor orig, Player self, AbstractCreature absCreature, World world) =>
            {
                orig(self, absCreature, world);

                if (SicknessLevel(absCreature) > 0)
                {
                    Debug.Log("player is sick");
                    self.slugcatStats.runspeedFac *= 0.8f;
                }
            };

            On.Player.Update += (On.Player.orig_Update orig, Player self, bool eu) =>
            {
                // if slugcat has food poisoning,
                // pretend that slugcat is malnourished for the
                // duration of the update call
                bool oldMalnourished = self.slugcatStats.malnourished;
                if (SicknessLevel(self.abstractCreature) > 0)
                    self.slugcatStats.malnourished = true;

                orig(self, eu);

                self.slugcatStats.malnourished = oldMalnourished;
            };

            On.Player.Regurgitate += (On.Player.orig_Regurgitate orig, Player self) =>
            {
                orig(self);
                GetPlayerData(self).isVomiting = false;
            };

            // create save data
            On.SaveState.SessionEnded += (
                On.SaveState.orig_SessionEnded orig, SaveState self, RainWorldGame game,
                bool survived, bool newMalnourished
            ) =>
            {
                if (survived)
                {
                    sicknessSaveData.Clear();
                    bool hasPoisoning = false;

                    foreach (var player in game.Players)
                    {
                        int level = DelayedSicknessLevel(player);
                        if (level > 0) hasPoisoning = true;
                        mod.logSource.LogDebug($"store sickness level of {level}");
                        sicknessSaveData.Add(level); // add one to level to worsen sickness    
                    }

                    // if no one has food poisoning, do not save
                    if (!hasPoisoning)
                        sicknessSaveData.Clear();
                    else
                        mod.logSource.LogDebug("save player poisoning");
                }

                orig(self, game, survived, newMalnourished);
            };

            // writing sickness save data to game save
            On.SaveState.SaveToString += (On.SaveState.orig_SaveToString orig, SaveState self) =>
            {
                string text = orig(self);

                if (sicknessSaveData.Count > 0)
                {
                    mod.logSource.LogDebug("Save food sickness");

                    text += "FOODSICKNESS<svB>";
                    text += string.Join(":", sicknessSaveData);
                    text += "<svA>";
                }
                return text;
            };

            // load sickness data from save string
            On.SaveState.LoadGame += (On.SaveState.orig_LoadGame orig, SaveState self, string saveStr, RainWorldGame game) =>
            {
                sicknessSaveData.Clear();

                orig(self, saveStr, game);

                // load sickness save data
                for (int i = self.unrecognizedSaveStrings.Count - 1; i >= 0; i--)
                {
                    string str = self.unrecognizedSaveStrings[i];

                    string[] subdiv = Regex.Split(str, "<svB>");

                    if (subdiv[0] == "FOODSICKNESS")
                    {
                        mod.logSource.LogDebug("found FOODSICKNESS");
                        
                        string[] data = subdiv[1].Split(':');
                        for (int j = 0; j < data.Length; j++)
                        {
                            sicknessSaveData.Add(int.Parse(data[j]));
                        }

                        self.unrecognizedSaveStrings.RemoveAt(i);
                    }
                }
            };

            // register player sickness from save data
            On.GameSession.AddPlayer += (On.GameSession.orig_AddPlayer orig, GameSession self, AbstractCreature player) =>
            {
                int playerIndex = self.Players.Count;
                orig(self, player);

                // load sickness data
                if (playerIndex < sicknessSaveData.Count)
                {
                    int sicknessLevel = sicknessSaveData[playerIndex];
                    sicknessData.Add(player, sicknessLevel);

                    mod.logSource.LogDebug($"player {playerIndex} sickness is {sicknessLevel}");

                    // lose one cycle of food poisoning
                    // one cycle of food poisoning = 2 levels
                    if (sicknessLevel > 0)
                        delayedSicknessData.Add(player, sicknessLevel - 2);
                }

                playerData.Add(new PlayerData());
            };

            // press 2 while in dev tools to begin force vomiting
            On.RainWorldGame.Update += (On.RainWorldGame.orig_Update orig, RainWorldGame self) =>
            {
                orig(self);

                if (self.devToolsActive)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha2))
                    {
                        Debug.Log("force regurgitate");
                        playerData[0].isVomiting = true;
                    }
                }
            };

            // TODO: slugpup food poisoning...
            //On.CreatureState.LoadFromString += CreatureState_LoadFromString;
            //On.CreatureState.ToString += CreatureState_ToString;
        }

        #endregion Hooks
    }
}