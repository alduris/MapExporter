namespace MapExporterNew
{
    public static class Preferences
    {
        public static readonly Preference<bool> ShowCreatures = new("show/creatures", false);
        public static readonly Preference<bool> ShowGhosts = new("show/ghosts", true);
        public static readonly Preference<bool> ShowGuardians = new("show/guadians", true);
        public static readonly Preference<bool> ShowInsects = new("show/insects", false);
        public static readonly Preference<bool> ShowOracles = new("show/oracles", true);
        public static readonly Preference<bool> ShowPrince = new("show/prince", true);

        public static readonly Preference<bool> ScreenshotterAutoFill = new("screenshotter/autofill", true);
        public static readonly Preference<bool> ScreenshotterSkipExisting = new("screenshotter/skipexisting", false);

        public static readonly Preference<bool> EditorCheckOverlap = new("editor/overlap", false);
        public static readonly Preference<bool> EditorShowCameras = new("editor/cameras", false);

        public static readonly Preference<bool> GeneratorLessIntense = new("generator/lessintensive", false);
        public static readonly Preference<int> GeneratorTargetFPS = new("generator/targetfps", 10, 5, 40);
        public static readonly Preference<int> GeneratorCacheSize = new("generator/cachesize", 64, 0, int.MaxValue);
        public static readonly Preference<bool> GeneratorSkipTiles = new("generator/skiptiles", false);

        public readonly struct Preference<T>(string key, T defaultValue)
        {
            public readonly string key = key;
            public readonly T defaultValue = defaultValue;

            public readonly bool hasRange = false;
            public readonly T minRange;
            public readonly T maxRange;

            public Preference(string key, T defaultValue, T min, T max) : this(key, defaultValue)
            {
                hasRange = true;
                minRange = min;
                maxRange = max;
            }

            public readonly T GetValue() => Data.UserPreferences.TryGetValue(key, out var obj) && obj is T val ? val : defaultValue;
        }
    }
}
