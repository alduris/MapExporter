namespace MapExporter.Tabs
{
    internal sealed class OIUtil : OptionInterface
    {
        private OIUtil() { }
        public readonly static OIUtil Instance = new();

        public static Configurable<T> CosmeticBind<T>(T init) => new(Instance, null, init, null);

        public const float SLIDER_WIDTH = 20f; // default width of scrollbar in OpScrollBox plus its offset from the side
    }
}
