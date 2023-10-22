using System;
using System.Collections.Generic;
using UnityEngine;
using MonoMod.Cil;
using Mono.Cecil.Cil;

namespace RWMod
{
    public class FoodSickness
    {
        private readonly static Dictionary<EntityID, int> sicknessData = new();

        private static RWMod mod;

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

        public static void Reset()
        {

        }

        public static void Cleanup()
        {
            sicknessData.Clear();
        }

        public static void NextCycle()
        {
            foreach (var key in sicknessData.Keys)
            {
                if (sicknessData.ContainsKey(key))
                {
                    sicknessData[key]++;
                }
            }
        }

        public static void Infect(AbstractCreature absCreature)
        {
            // if creature was not already sick
            if (!sicknessData.TryGetValue(absCreature.ID, out _))
            {
                // TODO: set sickness level to 1
                // then in next cycle, this sickness level increases
                sicknessData.Add(absCreature.ID, 2);
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
            On.Player.ctor += (On.Player.orig_ctor orig, Player self, AbstractCreature absCreature, World world) =>
            {
                orig(self, absCreature, world);

                if (SicknessLevel(absCreature) > 1)
                {
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

            On.RainWorldGame.Win += (On.RainWorldGame.orig_Win orig, RainWorldGame self, bool malnourished) =>
            {
                NextCycle();
                orig(self, malnourished);
            };

            // TODO: slugpup food poisoning...
            //On.CreatureState.LoadFromString += CreatureState_LoadFromString;
            //On.CreatureState.ToString += CreatureState_ToString;
        }

        /*
        private static string SaveState_SaveToString(On.SaveState.orig_SaveToString orig, SaveState self)
        {
            string baseString = orig(self);

            if (SicknessLevel(ac) > 1)
            {
                mod.logSource.LogInfo(baseString);
                baseString += $""
            }
            return baseString;
        }
        */

        #endregion Hooks
    }
}