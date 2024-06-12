using MapExporter.Tabs;
using Menu.Remix.MixedUI;

internal class UI : OptionInterface
{
    public UI()
    {
        // todo: configurable settings
        /**todo: configurable settings
         * list:
         * - show echoes
         * - show insects
         * - show rain room setting
         * - collect all placed objects
         * - skip existing rooms in generator tab
         * - skip already rendered rooms in screenshotter
         */
    }

    public override void Initialize()
    {
        base.Initialize();

        Tabs = [
            // General settings tab
            new ScreenshotTab(this),
            new EditTab(this),
            new GenerateTab(this),
            new ServerTab(this),
            // Export tab
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
