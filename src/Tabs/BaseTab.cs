using Menu.Remix.MixedUI;

namespace MapExporter.Tabs
{

    internal abstract class BaseTab(OptionInterface owner, string name) : OpTab(owner, name)
    {
        public abstract void Initialize();
        public abstract void Update();
    }
}
