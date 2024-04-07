using MapExporter.Tabs;
using Menu.Remix.MixedUI;

internal class UI : OptionInterface
{
    public UI()
    {
        /* TODO:
         * remix user interface where users can select regions and slugcats (optional) to render for, then the game opens up new instances of itself
         * but those versions actually get the screenshots and then once it's done, the game allows you to move around rooms and finally puts them
         * together in place of the python script to remove the dependency of having python installed
         */
    }

    public override void Initialize()
    {
        base.Initialize();

        Tabs = [
            new ScreenshotTab(this),
            new EditTab(this),
            // new GenerateTab(this)
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
