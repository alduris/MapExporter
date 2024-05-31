namespace MapExporter.Tabs
{
    internal sealed class OIUtil : OptionInterface
    {
        private OIUtil() { }
        public readonly static OIUtil Instance = new();

        public static Configurable<T> CosmeticBind<T>(T init) => new(Instance, null, init, null);
    }
}
