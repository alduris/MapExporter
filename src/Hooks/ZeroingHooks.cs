using MonoMod.RuntimeDetour;

namespace MapExporterNew.Hooks
{
    internal static class ZeroingHooks
    {
        public static void Apply()
        {
            new Hook(typeof(RainWorldGame).GetProperty("TimeSpeedFac").GetGetMethod(), RainWorldGame_ZeroProperty);
            new Hook(typeof(RainWorldGame).GetProperty("InitialBlackSeconds").GetGetMethod(), RainWorldGame_ZeroProperty);
            new Hook(typeof(RainWorldGame).GetProperty("FadeInTime").GetGetMethod(), RainWorldGame_ZeroProperty);
        }

        // zeroes some annoying fades
        private delegate float orig_PropertyToZero(RainWorldGame self);
        private static float RainWorldGame_ZeroProperty(orig_PropertyToZero _, RainWorldGame _1)
        {
            return 0f;
        }
    }
}
