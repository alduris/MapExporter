using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs
{

    internal abstract class BaseTab(OptionInterface owner, string name) : OpTab(owner, Custom.rainWorld.inGameTranslator.Translate("MapExportTabName:" + name))
    {
        public const float MENU_SIZE = 600f;
        public const float SCROLLBAR_WIDTH = 20f;
        public const float CHECKBOX_SIZE = 24f;
        public const float LINE_HEIGHT = 20f;
        public const float BIG_LINE_HEIGHT = 30f;

        public static readonly Color BlueColor = new(0.5f, 0.65f, 0.95f);
        public static readonly Color RedColor = new(0.85f, 0.5f, 0.55f);
        public static readonly Color YellowColor = new(0.95f, 0.9f, 0.65f);
        public static readonly Color GreenColor = new(0.65f, 0.95f, 0.8f);

        public abstract void Initialize();
        public abstract void Update();

        public string Translate(string key) => Custom.rainWorld.inGameTranslator.Translate(key);
    }
}
