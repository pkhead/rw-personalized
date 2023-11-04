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
        private readonly static Dictionary<EntityID, int> sicknessData = new();

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
                    cursor.EmitDelegate((PlayerGraphics self) => SicknessLevel(self.player.abstractCreature) > 1);
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
                    cursor.EmitDelegate((PlayerGraphics self) => SicknessLevel(self.player.abstractCreature) > 1);
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
        }

        public static void Cleanup()
        {
            mod.logSource.LogDebug("cleanup");
            sicknessData.Clear();
        }
        
        public static void Infect(AbstractCreature absCreature)
        {
            // if creature was not already sick
            if (SicknessLevel(absCreature) == 0)
            {
                Debug.Log("infect creature");
                sicknessData.Add(absCreature.ID, 1);
            }
        }

        public static int SicknessLevel(AbstractCreature absCreature)
        {
            if (sicknessData.TryGetValue(absCreature.ID, out var value))
                return value;

            return 0;
        }

        #region Hooks

        public static void ApplyHooks()
        {
            // player moves slower when they have food poisoning
            On.Player.ctor += (On.Player.orig_ctor orig, Player self, AbstractCreature absCreature, World world) =>
            {
                mod.logSource.LogDebug("Player ctor");

                orig(self, absCreature, world);

                if (SicknessLevel(absCreature) > 1)
                {
                    mod.logSource.LogDebug("player is sick");
                    self.slugcatStats.runspeedFac *= 0.8f;
                }
            };

            On.Player.Update += (On.Player.orig_Update orig, Player self, bool eu) =>
            {
                // if slugcat has food poisoning,
                // pretend that slugcat is malnourished for the
                // duration of the update call
                bool oldMalnourished = self.slugcatStats.malnourished;
                if (SicknessLevel(self.abstractCreature) > 1)
                    self.slugcatStats.malnourished = true;

                orig(self, eu);

                self.slugcatStats.malnourished = oldMalnourished;
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
                        int level = SicknessLevel(player);
                        if (level > 0) hasPoisoning = true;
                        sicknessSaveData.Add(level + 1); // add one to level to worsen sickness    
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
                mod.logSource.LogDebug("SaveToString called");

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
                mod.logSource.LogDebug("LoadGame called");
                sicknessSaveData.Clear();

                orig(self, saveStr, game);

                // load sickness save data
                foreach (var str in self.unrecognizedSaveStrings)
                {
                    string[] subdiv = Regex.Split(str, "<svB>");

                    if (subdiv[0] == "FOODSICKNESS")
                    {
                        mod.logSource.LogDebug("found FOODSICKNESS");
                        
                        string[] data = subdiv[1].Split(':');
                        for (int i = 0; i < data.Length; i++)
                        {
                            sicknessSaveData.Add(int.Parse(data[i]));
                        }
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
                    mod.logSource.LogDebug($"Player {playerIndex} = {sicknessSaveData[playerIndex]}");
                    sicknessData.Add(player.ID, sicknessSaveData[playerIndex]);
                }
            };

            // TODO: slugpup food poisoning...
            //On.CreatureState.LoadFromString += CreatureState_LoadFromString;
            //On.CreatureState.ToString += CreatureState_ToString;
        }

        #endregion Hooks
    }
}