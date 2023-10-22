using BepInEx;
using MonoMod.RuntimeDetour;
using UnityEngine;
using System.Reflection;

namespace RWMod
{
    public partial class RWMod : BaseUnityPlugin
    {
        private void DangleFruitHooks()
        {
            On.DangleFruit.ctor += DangleFruit_ctor;
            On.DangleFruit.ApplyPalette += DangleFruit_ApplyPalette;
            On.DangleFruit.BitByPlayer += DangleFruit_BitByPlayer;

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
            
            // make shiny or rotten
            var entityData = GetEntityData(self.abstractPhysicalObject);

            if (Random.value < RottenChance)
            {
                entityData.isRotten = true;
            }
            else if (Random.value < ShinyChance)
            {
                entityData.isShiny = true;
            }
            
            // restore random generator to old state
            Random.state = state;
        }

        private void DangleFruit_ApplyPalette(On.DangleFruit.orig_ApplyPalette orig, DangleFruit self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
        {
            // call original code
            orig(self, sLeaser, rCam, palette);
            var entityData = GetEntityData(self.abstractPhysicalObject);
            
            if (entityData.isRotten)
            {
                self.color = new Color(47f / 255f, 87f / 255f, 36 / 255f);
            }
            if (entityData.isShiny)
            {
                self.color = RainWorld.SaturatedGold;
            }
        }

        private void DangleFruit_BitByPlayer(On.DangleFruit.orig_BitByPlayer orig, DangleFruit self, Creature.Grasp grasp, bool eu)
        {
            orig(self, grasp, eu);
            var entityData = GetEntityData(self.abstractPhysicalObject);

            if (self.bites < 1 && entityData.isRotten)
            {
                Debug.Log("Player ate rotten fruit");
                // TODO: make sick
            }
        }

        public delegate int orig_FoodPoints(DangleFruit self);
        public int DangleFruit_FoodPoints_get(orig_FoodPoints orig, DangleFruit self)
        {
            if (GetEntityData(self.abstractPhysicalObject).isShiny)
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