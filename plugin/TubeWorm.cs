using System.Collections.Generic;
using BepInEx;
using RWCustom;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

namespace RWMod
{
    public partial class RWMod : BaseUnityPlugin
    {
        private void TubeWorm_ctor(On.TubeWorm.orig_ctor orig, TubeWorm self, AbstractCreature creature, World world)
        {
            orig(self, creature, world);

            Random.State oldState = Random.state;
            Random.InitState(creature.ID.RandomSeed);

            // determine if tube worm is shiny
            isShiny[self.abstractCreature.ID] = Random.value < ShinyChance;

            Random.state = oldState;       
        }

        private void TubeWormGraphics_ctor(On.TubeWormGraphics.orig_ctor orig, TubeWormGraphics self, PhysicalObject obj)
        {
            orig(self, obj);

            // on random chance, make color yellow instead of blue
            Random.State oldState = Random.state;
            Random.InitState(self.worm.abstractCreature.ID.RandomSeed);

            if (IsCreatureShiny(self.worm.abstractCreature))
            {
                self.color = Custom.HSL2RGB(
                    Mathf.Lerp(42f, 59f, Random.value) / 360f,
                    Mathf.Lerp(0.4f, 0.9f, Random.value),
                    Mathf.Lerp(0.15f, 0.3f, Random.value)
                );
            }

            // restore old state of rng
            Random.state = oldState;
        }

        private void TubeWormGraphics_ApplyPalette(
            On.TubeWormGraphics.orig_ApplyPalette orig,
            TubeWormGraphics self,
            RoomCamera.SpriteLeaser sLeaser,
            RoomCamera rCam,
            RoomPalette palette
        )
        {
            // make stripes white instead of black
            orig(self, sLeaser, rCam, palette);

            if (IsCreatureShiny(self.worm.abstractCreature))
            {
                sLeaser.sprites[1].color = new Color(0.9f, 0.9f, 0.9f);
            }
        }




    }
}
