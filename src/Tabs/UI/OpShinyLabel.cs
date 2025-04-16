using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporterNew.Tabs.UI
{
    /// <summary>
    /// Quite a shrimple class, just applies the MenuText shader to a label
    /// </summary>
    internal class OpShinyLabel : OpLabel
    {
        public OpShinyLabel(float posX, float posY, string text = "TEXT", bool bigText = false) : base(posX, posY, text, bigText)
        {
            label.shader = Menu.manager.rainWorld.Shaders["MenuText"];
        }

        public OpShinyLabel(Vector2 pos, Vector2 size, string text = "TEXT", FLabelAlignment alignment = FLabelAlignment.Center, bool bigText = false, FTextParams textParams = null) : base(pos, size, text, alignment, bigText, textParams)
        {
            label.shader = Menu.manager.rainWorld.Shaders["MenuText"];
        }
    }
}
