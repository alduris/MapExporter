using Menu.Remix.MixedUI;

namespace MapExporter.Tabs
{
    internal sealed class OIUtil : OptionInterface
    {
        private OIUtil() { }
        public readonly static OIUtil Instance = new();

        public static Configurable<T> CosmeticBind<T>(T init) => new(Instance, null, init, null);

        public static float FONT_HEIGHT = LabelTest._lineHeight;
        public const float LABEL_HEIGHT = 20f;
        public const float BIG_LABEL_HEIGHT = 30f;
        public const float COMBOBOX_HEIGHT = 24f;
        public const float CHECKBOX_HEIGHT = 24f;
        public const float TEXTBOX_HEIGHT = 24f;
        public const float UPDOWN_HEIGHT = 30f;
        public const float SLIDER_HEIGHT = 30f;
        public const float DRAGGER_HEIGHT = 24f;
        public const float COLOR_HEIGHT = 150f; // I'm just putting all the inputs here, why would I ever need this lmao
    }
}
