using MapExporter.Tabs;
using Menu.Remix.MixedUI;

internal class UI : OptionInterface
{
    public override void Initialize()
    {
        base.Initialize();

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
    }
}
