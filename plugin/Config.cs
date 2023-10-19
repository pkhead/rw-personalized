using Menu.Remix.MixedUI;
using UnityEngine;

namespace RWMod
{
    public class Options : OptionInterface
    {
        public static Options instance = new Options();

        public static Configurable<int> ShinyChance = instance.config.Bind(
            key: "shinyChance",
            defaultValue: 10,
            info: new ConfigurableInfo(
                "The chance that some creatures will be \"shiny\"",
                new ConfigAcceptableRange<int>(0, 100),
                "",
                "ShinyChance"
            )
        );

        public static Configurable<int> SpawnChance = instance.config.Bind(
            key: "spawnChance",
            defaultValue: 5,
            info: new ConfigurableInfo(
                "The chance that IT will spawn",
                new ConfigAcceptableRange<int>(0, 100),
                "",
                "SpawnChance"
            )
        );

        public Options()
        {}

        public override void Initialize()
        {
            base.Initialize();
            tabIndex = -1;
            curY = MENU_TOP - ITEM_HEIGHT;
            curX = 5;
            Tabs = new OpTab[1];
            
            AddTab("General");
            AddSlider("Shiny Chance", "The chance that some creatures will be \"shiny\"", ShinyChance, 160);
            AddSlider("Spawn Chance", "The chance that IT will spawn", SpawnChance, 160);
        }

        #region UI Builder functions

        private const float ITEM_HEIGHT = 30f;
        private const float ITEM_MARGIN_Y = 5f;
        private const float LABEL_MARGIN_X = 20f;
        private const float MENU_TOP = 500f;

        private int tabIndex = -1;
        private float curY = MENU_TOP - ITEM_HEIGHT;
        private float curX = 5;

        private void AddTab(string tabName)
        {
            Tabs[++tabIndex] = new OpTab(this, tabName);
        }

        private void AddSlider(string text, string desc, Configurable<int> config, int width)
        {
            var slider = new OpSlider(config, new Vector2(curX, curY), width);
            var label = new OpLabel(curX + slider.size.x + LABEL_MARGIN_X, curY + (slider.size.y - LabelTest.LineHeight(false)) / 2, text, false);
            Tabs[tabIndex].AddItems(slider, label);
            curY -= slider.size.y + ITEM_MARGIN_Y;
        }

        #endregion
    }
}