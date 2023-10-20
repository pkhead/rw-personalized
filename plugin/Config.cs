using System.Reflection;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace RWMod
{
    public class Options : OptionInterface
    {
        public static Options instance = new Options();

        public static Configurable<int> ShinyChance = instance.config.Bind(
            key: "shinyChance",
            defaultValue: 20,
            info: new ConfigurableInfo(
                "The chance that some creatures will be \"shiny\"",
                new ConfigAcceptableRange<int>(0, 100),
                "",
                "ShinyChance"
            )
        );

        public static Configurable<int> SpawnChance = instance.config.Bind(
            key: "spawnChance",
            defaultValue: 2,
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
            InitBuilder(1);

            AddTab("General");
            Title("Personalizer");
            AddSlider("Shiny Chance", "The chance that some creatures will be differently colored", ShinyChance, 160);
            AddSlider("Spawn Chance", "The chance that it will spawn", SpawnChance, 160);
        }

        #region UI Builder functions

        private const float ITEM_HEIGHT = 30f;
        private const float ITEM_MARGIN_Y = 5f;
        private const float LABEL_MARGIN_X = 20f;
        private const float MENU_TOP = 600f - 50f;
        private const float MENU_RIGHT = 600f; // i don't really know

        private int tabIndex = -1;
        private float curY = MENU_TOP - ITEM_HEIGHT;
        private float curX = 5;

        private void InitBuilder(int tabCount)
        {
            tabIndex = -1;
            curY = MENU_TOP - 60;
            curX = 0;
            Tabs = new OpTab[tabCount];
        }

        private void AddTab(string tabName)
        {
            Tabs[++tabIndex] = new OpTab(this, tabName);
        }

        private void Title(string text)
        {
            var label = new OpLabel(
                new Vector2(0, MENU_TOP - 10f),
                new Vector2(MENU_RIGHT, 10f), text, FLabelAlignment.Center, true);
            
            Tabs[tabIndex].AddItems(label);
        }

        private void AddSlider(string text, string desc, Configurable<int> config, int width)
        {
            var slider = new OpSlider(config, new Vector2(curX, curY), width) {
                description = desc
            };

            var label = new OpLabel(curX + slider.size.x + LABEL_MARGIN_X, curY + (slider.size.y - LabelTest.LineHeight(false)) / 2, text, false);
            Tabs[tabIndex].AddItems(slider, label);
            curY -= slider.size.y + ITEM_MARGIN_Y;
        }

        #endregion
    }
}