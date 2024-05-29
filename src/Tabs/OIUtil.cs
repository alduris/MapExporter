using System.Runtime.CompilerServices;
using Menu.Remix.MixedUI;

namespace MapExporter.Tabs
{
    internal sealed class OIUtil : OptionInterface
    {
        private OIUtil() { }
        public readonly static OIUtil Instance = new();

        public static Configurable<T> CosmeticBind<T>(T init) => new(Instance, null, init, null);

        public const float SLIDER_WIDTH = 15f; // default width of scrollbar in OpScrollBox


        ///////////////////////////////////////////////////

        private static ConditionalWeakTable<OpSimpleButton, object> clearButtonCWT = new();
        public static void AddClearButton(OpSimpleButton button) => clearButtonCWT.Add(button, new());

        public static void ApplyHooks()
        {
            try
            {
                On.Menu.Remix.MixedUI.OpSimpleButton.GrafUpdate += OpSimpleButton_GrafUpdate;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError(ex);
            }
        }

        private static void OpSimpleButton_GrafUpdate(On.Menu.Remix.MixedUI.OpSimpleButton.orig_GrafUpdate orig, OpSimpleButton self, float timeStacker)
        {
            orig(self, timeStacker);
            if (clearButtonCWT.TryGetValue(self, out _))
            {
                self._rect.Hide();
                self._rectH.Hide();
            }
        }
    }
}
