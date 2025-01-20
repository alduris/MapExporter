using MapExporter.Server;
using MapExporter.Tabs;

namespace MapExporter
{
    internal class UI : OptionInterface
    {
        public override void Initialize()
        {
            base.Initialize();

            Resources.CopyFrontendFiles();

            Tabs = [
                new MainMenuTab(this),
                // new DataTab(this),
                new ScreenshotTab(this),
                new EditTab(this),
                new GenerateTab(this),
                new ServerTab(this)
            ];

            foreach (var tab in Tabs)
            {
                (tab as BaseTab).Initialize();
            }
        }

        public override void Update()
        {
            base.Update();

            foreach (var tab in Tabs)
            {
                if (!tab.isInactive)
                {
                    (tab as BaseTab).Update();
                }
            }
            Exporter.UpdateFileCounter();
        }
    }
}
