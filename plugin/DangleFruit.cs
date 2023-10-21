using BepInEx;
using MonoMod.RuntimeDetour;
using System.Reflection;
using Random = UnityEngine.Random;

namespace RWMod
{
    public partial class RWMod : BaseUnityPlugin
    {
        private void DangleFruitHooks()
        {
            On.DangleFruit.ctor += DangleFruit_ctor;
            On.DangleFruit.ApplyPalette += DangleFruit_ApplyPalette;

            Hook dangleFruitHook = new(
                typeof(DangleFruit).GetProperty("FoodPoints", BindingFlags.Instance | BindingFlags.Public).GetGetMethod(),
                typeof(RWMod).GetMethod("DangleFruit_FoodPoints_get", BindingFlags.Instance | BindingFlags.Public),
                this
            );
        }

        private void DangleFruit_ctor(On.DangleFruit.orig_ctor orig, DangleFruit self, AbstractPhysicalObject abstractPhysicalObject)
        {
            // call original code
            orig(self, abstractPhysicalObject);

            // save old state of rng
            Random.State state = Random.state;
            
            // seed random generator to unique item ID
            int randomSeed = abstractPhysicalObject.ID.RandomSeed;
            Random.InitState(randomSeed);
            
            // make shiny
            isShiny[abstractPhysicalObject.ID] = Random.value < ShinyChance;
            
            // restore random generator to old state
            Random.state = state;
        }

        private void DangleFruit_ApplyPalette(On.DangleFruit.orig_ApplyPalette orig, DangleFruit self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            // call original code
            orig(self, sLeaser, rCam, palette);
            
            if (IsShiny(self.abstractPhysicalObject))
            {
                self.color = RainWorld.SaturatedGold;
            }
        }

        public delegate int orig_FoodPoints(DangleFruit self);
        public int DangleFruit_FoodPoints_get(orig_FoodPoints orig, DangleFruit self)
        {
            if (IsShiny(self.abstractPhysicalObject))
            {
                return 2;
            }
            else
            {
                return orig(self);
            }
        }
    }
}