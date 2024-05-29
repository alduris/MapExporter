using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    public class OpTextButton(Vector2 pos, Vector2 size, string displayText = "") : OpSimpleButton(pos, size, displayText)
    {
        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);
            _rect.Hide();
            _rectH.Hide();
        }
    }
}
