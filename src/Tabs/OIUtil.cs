using System;
using Menu.Remix.MixedUI;

namespace MapExporter.Tabs
{
    internal sealed class OIUtil : OptionInterface
    {
        private OIUtil() { }
        public readonly static OIUtil Instance = new();

        public static Configurable<T> CosmeticBind<T>(T init) => new(Instance, null, init, null);
        public static Configurable<T> CosmeticRange<T>(T val, T min, T max) where T : IComparable => new(val, new ConfigAcceptableRange<T>(min, max));
    }
}
