using MapExporter.Tabs.UI;
using Menu;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal class MainMenuTab(OptionInterface owner) : BaseTab(owner, "Main Menu")
    {
        const string HOW_TO_STRING = "1. Run screenshotter on region\n2. Edit layout\n3. Finalize in generator tab\n4. Test interactive map\n5. Export";
        public override void Initialize()
        {
            AddItems(
                // Title
                new OpShinyLabel(new Vector2(0f, 570f), new Vector2(600f, 30f), "MAP EXPORTER", FLabelAlignment.Center, true),
                new OpImage(new Vector2(0f, 559f), "pixel") { scale = new Vector2(600f, 2f), color = MenuColorEffect.rgbMediumGrey },

                // How to
                new OpLabel(10f, 520f, "HOW TO USE", true),
                new OpLabelLong(new Vector2(10f, 445f), new Vector2(280f, 75f), HOW_TO_STRING, false, FLabelAlignment.Left), // line height = 15f

                new OpImage(new Vector2(299f, 435f), "pixel") { scale = new Vector2(2f, 550f - 425f), color = MenuColorEffect.rgbMediumGrey },

                // Statistics idk something in this section
                new OpLabel(310f, 520f, "HEADING", true),

                // Options
                new OpImage(new Vector2(0f, 424f), "pixel") { scale = new Vector2(600f, 2f), color = MenuColorEffect.rgbMediumGrey },
                new OpLabel(new Vector2(0f, 385f), new Vector2(600f, 30f), "OPTIONS", FLabelAlignment.Center, true)
                // Showing things: insects, echoes, iterators, guardians, creatures in screenshots
                // Code things: re-screenshotted rooms, skip existing tiles (generator), less resource intensive (generator), auto-fill slugcats styles
                // Data things: reset screenshots, reset generator output (I should really come up with a better name for that tab lol), reset everything
            );
        }

        public override void Update() { }
    }
}
