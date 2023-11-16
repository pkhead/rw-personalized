using System;
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

        public float RottenChance {
            get => (float) Options.RottenChance.Value / 100f;
        }

        class EntityData
        {
            public bool isShiny = false;
            public bool isRotten = false;
        }

        // if TubeWorm, then it renders the tube worm as yellow/white
        // if BlueLizard, then it renders the lizard as yellow/white
        // if a SpitLizard, then it renders the lizard as black/blue
        private readonly Dictionary<EntityID, EntityData> entityData = new();
        private bool isInit = false;
        public BepInEx.Logging.ManualLogSource logSource;

        public RWMod() {}

        // create class for mod-specific data associated with this entity
        // if data for this entity already exists, then return the previously
        // created class.
        // this table will be cleared at the start of every game session
        private EntityData MakeEntityData(AbstractPhysicalObject entity)
        {
            EntityData data;

            if (entityData.TryGetValue(entity.ID, out data))
            {
                return data;
            }
            else
            {
                data = new();
                entityData.Add(entity.ID, data);
                return data;
            }
        }

        private bool TryGetEntityData(AbstractPhysicalObject entity, out EntityData data)
        {
            return entityData.TryGetValue(entity.ID, out data);
        }

        public void OnEnable()
        {
            On.RainWorld.OnModsInit += RainWorld_OnModsInit;
            //On.RainWorld.OnModsDisabled += RainWorld_OnModsDisabled;

            logSource = BepInEx.Logging.Logger.CreateLogSource("Personalized");

            It.Init(this);
            FoodSickness.Init(this);
        }

        private void RainWorld_OnModsDisabled(On.RainWorld.orig_OnModsDisabled orig, RainWorld self, ModManager.Mod[] newlyDisabledMods)
        {
            orig(self, newlyDisabledMods);
        }

        private void RainWorld_OnModsInit(On.RainWorld.orig_OnModsInit orig, RainWorld self)
        {
            orig(self);

            try
            {
                MachineConnector.SetRegisteredOI(MOD_ID, Options.instance);
                
                if (isInit) return;
                isInit = true;

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

                // other
                It.ApplyHooks();
                FoodSickness.ApplyHooks();
            }
            catch (Exception e)
            {
                logSource.LogError(e.Message);
            }
        }
        
        private void Cleanup()
        {
            entityData.Clear();
            It.Cleanup();
            FoodSickness.Cleanup();
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
            Cleanup();
            It.Reset();
        }
    }
}
