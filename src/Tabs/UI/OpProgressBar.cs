using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    internal class OpProgressBar(Vector2 pos, float width) : OpRect(pos, new Vector2(width, 14f), 0.3f)
    {
        public OpRect inner;

        public void Initialize()
        {
            inner = new OpRect(pos, new Vector2(14f, 14f), 1f)
            {
                colorFill = colorEdge,
                colorEdge = colorEdge,
            };
            tab.AddItems(inner);
        }

        public void Update(float progress)
        {
            inner.size.Set(Mathf.Max(14f, size.x * progress), inner.size.y);
            inner.Change();
        }

        public override void Update()
        {
            base.Update();
            if (inner != null)
            {
                if (colorEdge != inner.colorEdge)
                {
                    inner.colorEdge = colorEdge;
                    inner.colorFill = colorFill;
                }
                if (pos != inner.pos)
                {
                    inner.pos = pos;
                }
            }
        }
    }
}
