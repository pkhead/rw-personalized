﻿using System;
using System.Collections.Generic;
using System.Security;
using System.Security.Permissions;
using BepInEx;

#pragma warning disable CS0618
[module: UnverifiableCode]
[assembly: SecurityPermission( SecurityAction.RequestMinimum, SkipVerification = true )]

namespace RWMod {
    [BepInPlugin(MOD_ID, "Personalizer", VERSION)]
    public partial class RWMod : BaseUnityPlugin
    {
        public const string MOD_ID = "pkhead.personalizer";
        public const string AUTHOR = "pkhead";
        public const string VERSION = "1.1";

        // chance that some specific creatures is "shiny"
        public float ShinyChance {
            get => (float) Options.ShinyChance.Value / 100f;
        }

        // if TubeWorm, then it renders the tube worm as yellow/white
        // if BlueLizard, then it renders the lizard as yellow/white
        // if a SpitLizard, then it renders the lizard as black/blue
        private readonly Dictionary<EntityID, bool> isShiny = new Dictionary<EntityID, bool>();

        private bool isInit = false;
        public BepInEx.Logging.ManualLogSource logSource;

        public RWMod() {}

        private bool IsShiny(AbstractPhysicalObject creature)
        {
            return isShiny.TryGetValue(creature.ID, out bool creatureIsShiny) && creatureIsShiny;
        }

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
            //On.RainWorld.OnModsDisabled += RainWorld_OnModsDisabled;

            logSource = BepInEx.Logging.Logger.CreateLogSource("Personalized");

            It.Init(this);
        }

        private void RainWorld_OnModsDisabled(On.RainWorld.orig_OnModsDisabled orig, RainWorld self, ModManager.Mod[] newlyDisabledMods)
        {
            orig(self, newlyDisabledMods);
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);
            if (isInit) return;
            isInit = true;

            try
            {
                MachineConnector.SetRegisteredOI(MOD_ID, Options.instance);

                // cleanup hooks
                On.RainWorldGame.ShutDownProcess += RainWorldGame_ShutDownProcess;
                On.GameSession.ctor += GameSession_ctor;

                // tube worm graphics
                On.TubeWormGraphics.ctor += TubeWormGraphics_ctor;
                On.TubeWormGraphics.ApplyPalette += TubeWormGraphics_ApplyPalette;

                // lizard graphics
                On.Lizard.ctor += Lizard_ctor;
                On.LizardGraphics.ctor += LizardGraphics_ctor;
                On.LizardGraphics.ApplyPalette += LizardGraphics_ApplyPalette;

                // bluefruit
                DangleFruitHooks();
                It.ApplyHooks();
            }
            catch (Exception e)
            {
                logSource.LogError(e.Message);
            }
        }
        
        private void Cleanup()
        {
            isShiny.Clear();
            It.Cleanup();
        }

        // cleanup hooks
        private void RainWorldGame_ShutDownProcess(On.RainWorldGame.orig_ShutDownProcess orig, RainWorldGame self)
        {
            orig(self);
            Cleanup();
        }

        private void GameSession_ctor(On.GameSession.orig_ctor orig, GameSession self, RainWorldGame game)
        {
            orig(self, game);
            It.Reset();
            Cleanup();
        }
    }
}
