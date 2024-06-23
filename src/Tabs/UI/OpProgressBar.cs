using Menu.Remix.MixedUI;
using UnityEngine;

namespace MapExporter.Tabs.UI
{
    internal class OpProgressBar(Vector2 pos, float width) : OpRect(pos, new Vector2(width, HEIGHT), 0.3f)
    {
        private const float HEIGHT = 16f;
        public OpRect inner;

        public void Initialize()
        {
            inner = new OpRect(pos, new Vector2(HEIGHT, HEIGHT), 1f)
            {
                colorFill = colorEdge,
                colorEdge = colorEdge,
            };

            if (InScrollBox)
            {
                scrollBox.AddItems(inner);
            }
            else
            {
                tab.AddItems(inner);
            }
        }

        public void Update(float progress)
        {
            inner.size = new Vector2(Mathf.Max(HEIGHT, size.x * progress), inner.size.y);
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
