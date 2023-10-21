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



    }
}















/*private void BlueFruitShiny(On.DangleFruit.orig_ApplyPalette orig, DangleFruit self, RoomCamera.SpriteLeaser sLeaser, RoomCamera rCam, RoomPalette palette)
{
    //this does something but i have no idea what
    int randomSeed = self.abstractPhysicalObject.ID.RandomSeed;
    UnityEngine.Random.State state = UnityEngine.Random.state;
    UnityEngine.Random.InitState(randomSeed);
    //random value
    float value = UnityEngine.Random.value;
    //compare randomized number to anything
    bool flag3 = value < 100;
    //the if/else should be self explanitory
    if (flag3)
    {
        //sets fruit color to look like one from rubicon (you can set this to whatever you like)
        this.color = Color.Lerp(RainWorld.SaturatedGold, palette.blackColor, this.darkness);
    }
    else
    {
        //default blue-fruit code
        sLeaser.sprites[0].color = palette.blackColor;
        if (ModManager.MSC && rCam.room.game.session is StoryGameSession && rCam.room.world.name == "HR")
        {
            this.color = Color.Lerp(RainWorld.SaturatedGold, palette.blackColor, this.darkness);
            return;
        }

        this.color = Color.Lerp(new Color(0f, 0f, 1f), palette.blackColor, this.darkness);
    }
    //this remains a mystery to be solved
    UnityEngine.Random.state = state;
}*/