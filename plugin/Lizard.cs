using BepInEx;
using UnityEngine;
using RWCustom;

namespace RWMod
{
    public partial class RWMod : BaseUnityPlugin
    {
        private void Lizard_ctor(On.Lizard.orig_ctor orig, Lizard self, AbstractCreature creature, World world)
        {
            orig(self, creature, world);

            Random.State oldState = Random.state;
            Random.InitState(creature.ID.RandomSeed);
            var entityData = MakeEntityData(self.abstractCreature);

            // determine if lizard is shiny
            entityData.isShiny = Random.value < ShinyChance;

            if (entityData.isShiny)
            {
                // shiny blue lizard, so make the head yellow
                if (self.Template.type == CreatureTemplate.Type.BlueLizard)
                {
                    self.effectColor = Custom.HSL2RGB(
                        Mathf.Lerp(42f, 59f, Random.value) / 360f,
                        1f,
                        Custom.ClampedRandomVariation(0.5f, 0.15f, 0.1f)
                    );
                }
            }

            Random.state = oldState;            
        }

        private void LizardGraphics_ctor(On.LizardGraphics.orig_ctor orig, LizardGraphics self, PhysicalObject ow)
        {
            orig(self, ow);

            // if this is a caramel lizard, then there is a chance that it will be
            // rendered as black/blue instead
            if (ModManager.MSC)
            {
                if (self.Caramel && TryGetEntityData(self.lizard.abstractCreature, out var data) && data.isShiny)
                {
                    self.lizard.effectColor = new Color(0.0f, 0.0f, 1.0f);
                }
            }
        }

        private void LizardGraphics_ApplyPalette(
            On.LizardGraphics.orig_ApplyPalette orig,
            LizardGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            RoomPalette palette
        ) {
            orig(self, sLeaser, rCam, palette);

            if (!self.debugVisualization && TryGetEntityData(self.lizard.abstractCreature, out var data) && data.isShiny)
            {
                // color body of shiny spit lizard (black)
                if (ModManager.MSC && self.Caramel)
                {
                    self.ColorBody(sLeaser, palette.blackColor);
                }

                // color body of shiny blue lizard (white)
                else if (self.lizard.Template.type == CreatureTemplate.Type.BlueLizard)
                {
                    self.ColorBody(sLeaser, new Color(0.9f, 0.9f, 0.9f));
                }
            }
        }
    }
}