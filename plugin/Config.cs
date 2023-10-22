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

        public static Configurable<int> RottenChance = instance.config.Bind(
            key: "rottenChance",
            defaultValue: 20,
            info: new ConfigurableInfo(
                "The chance that some objects will be rotten",
                new ConfigAcceptableRange<int>(0, 100),
                "",
                "RottenChance"
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

        public static Configurable<int> ItVolume = instance.config.Bind(
            key: "itVolume",
            defaultValue: 100,
            info: new ConfigurableInfo(
                "The volume of IT",
                new ConfigAcceptableRange<int>(0, 100),
                "",
                "ItVolume"
            )
        );

        public Options()
        {}

        public override void Initialize()
        {
            base.Initialize();
            InitBuilder(2);

            AddTab("Level 1");
            Title("Level 1");
            AddSlider("Shiny Chance", "The chance that some creatures will be differently colored", ShinyChance, 160);
            AddSlider("Rotten Chance", "The chance that some objects will be rotten", RottenChance, 160);

            AddTab("Level 2", new Color(0.85f, 0.35f, 0.4f));
            Title("Level 2");
            AddSlider("Spawn Chance", "The chance that it will spawn", SpawnChance, 160);
            AddSlider("Volume", "The volume of it", ItVolume, 160);
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

        private OpTab AddTab(string tabName, Color color)
        {
            var tab = AddTab(tabName);
            tab.colorButton = color;
            return tab;
        }

        private OpTab AddTab(string tabName)
        {
            var tab = new OpTab(this, tabName);
            Tabs[++tabIndex] = tab;
            curY = MENU_TOP - 60;
            curX = 0;
            
            return tab;
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