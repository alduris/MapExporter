using UnityEngine;

namespace MapExporterNew.Screenshotter.UI
{
    internal class PopupRoundedRect : IPopupUI
    {
        private readonly FSprite[] sprites;

        public Vector2 pos;
        public Vector2 size;
        public bool filled;
        public float fillAlpha;
        public Color bgColor = new Color(0f, 0f, 0f);
        public Color borderColor = Color.white;

        private int SideSprite(int side) => (filled ? 9 : 0) + side;
        private int CornerSprite(int corner) => (filled ? 9 : 0) + 4 + corner;
        private int FillSideSprite(int side) => side;
        private int FillCornerSprite(int corner) => 4 + corner;

        private const int MainFillSprite = 8;

        public PopupRoundedRect(FContainer container, Vector2 pos, Vector2 size, bool filled, float fillAlpha = 0.75f)
        {
            this.pos = pos;
            this.size = size;
            this.filled = filled;
            this.fillAlpha = fillAlpha;

            sprites = new FSprite[filled ? 17 : 8];
            for (int i = 0; i < 4; i++)
            {
                sprites[SideSprite(i)] = new FSprite("pixel", true)
                {
                    scaleY = 2f,
                    scaleX = 2f
                };
                sprites[CornerSprite(i)] = new FSprite("UIroundedCorner", true);
                if (filled)
                {
                    sprites[FillSideSprite(i)] = new FSprite("pixel", true)
                    {
                        scaleY = 6f,
                        scaleX = 6f
                    };
                    sprites[FillCornerSprite(i)] = new FSprite("UIroundedCornerInside", true);
                }
            }
            sprites[SideSprite(0)].anchorY = 0f;
            sprites[SideSprite(2)].anchorY = 0f;
            sprites[SideSprite(1)].anchorX = 0f;
            sprites[SideSprite(3)].anchorX = 0f;
            sprites[CornerSprite(0)].scaleY = -1f;
            sprites[CornerSprite(2)].scaleX = -1f;
            sprites[CornerSprite(3)].scaleY = -1f;
            sprites[CornerSprite(3)].scaleX = -1f;
            if (filled)
            {
                sprites[MainFillSprite] = new FSprite("pixel", true)
                {
                    anchorY = 0f,
                    anchorX = 0f
                };
                sprites[FillSideSprite(0)].anchorY = 0f;
                sprites[FillSideSprite(2)].anchorY = 0f;
                sprites[FillSideSprite(1)].anchorX = 0f;
                sprites[FillSideSprite(3)].anchorX = 0f;
                sprites[FillCornerSprite(0)].scaleY = -1f;
                sprites[FillCornerSprite(2)].scaleX = -1f;
                sprites[FillCornerSprite(3)].scaleY = -1f;
                sprites[FillCornerSprite(3)].scaleX = -1f;
                for (int j = 0; j < 9; j++)
                {
                    sprites[j].color = bgColor;
                    sprites[j].alpha = fillAlpha;
                }
            }

            foreach (var sprite in sprites)
            {
                container.AddChild(sprite);
            }
        }

        public void Update()
        {
            Vector2 drawPos = pos;
            Vector2 drawSize = size;
            drawPos -= size / 2f;
            drawPos = new Vector2(Mathf.Floor(drawPos.x) + 0.41f, Mathf.Floor(drawPos.y) + 0.41f);
            sprites[SideSprite(0)].x = drawPos.x + 1f;
            sprites[SideSprite(0)].y = drawPos.y + 7f;
            sprites[SideSprite(0)].scaleY = drawSize.y - 14f;
            sprites[SideSprite(1)].x = drawPos.x + 7f;
            sprites[SideSprite(1)].y = drawPos.y + drawSize.y - 1f;
            sprites[SideSprite(1)].scaleX = drawSize.x - 14f;
            sprites[SideSprite(2)].x = drawPos.x + drawSize.x - 1f;
            sprites[SideSprite(2)].y = drawPos.y + 7f;
            sprites[SideSprite(2)].scaleY = drawSize.y - 14f;
            sprites[SideSprite(3)].x = drawPos.x + 7f;
            sprites[SideSprite(3)].y = drawPos.y + 1f;
            sprites[SideSprite(3)].scaleX = drawSize.x - 14f;
            sprites[CornerSprite(0)].x = drawPos.x + 3.5f;
            sprites[CornerSprite(0)].y = drawPos.y + 3.5f;
            sprites[CornerSprite(1)].x = drawPos.x + 3.5f;
            sprites[CornerSprite(1)].y = drawPos.y + drawSize.y - 3.5f;
            sprites[CornerSprite(2)].x = drawPos.x + drawSize.x - 3.5f;
            sprites[CornerSprite(2)].y = drawPos.y + drawSize.y - 3.5f;
            sprites[CornerSprite(3)].x = drawPos.x + drawSize.x - 3.5f;
            sprites[CornerSprite(3)].y = drawPos.y + 3.5f;
            for (int i = 0; i < 4; i++)
            {
                sprites[SideSprite(i)].color = borderColor;
                sprites[CornerSprite(i)].color = borderColor;
            }
            if (filled)
            {
                sprites[FillSideSprite(0)].x = drawPos.x + 4f;
                sprites[FillSideSprite(0)].y = drawPos.y + 7f;
                sprites[FillSideSprite(0)].scaleY = drawSize.y - 14f;
                sprites[FillSideSprite(1)].x = drawPos.x + 7f;
                sprites[FillSideSprite(1)].y = drawPos.y + drawSize.y - 4f;
                sprites[FillSideSprite(1)].scaleX = drawSize.x - 14f;
                sprites[FillSideSprite(2)].x = drawPos.x + drawSize.x - 4f;
                sprites[FillSideSprite(2)].y = drawPos.y + 7f;
                sprites[FillSideSprite(2)].scaleY = drawSize.y - 14f;
                sprites[FillSideSprite(3)].x = drawPos.x + 7f;
                sprites[FillSideSprite(3)].y = drawPos.y + 4f;
                sprites[FillSideSprite(3)].scaleX = drawSize.x - 14f;
                sprites[FillCornerSprite(0)].x = drawPos.x + 3.5f;
                sprites[FillCornerSprite(0)].y = drawPos.y + 3.5f;
                sprites[FillCornerSprite(1)].x = drawPos.x + 3.5f;
                sprites[FillCornerSprite(1)].y = drawPos.y + drawSize.y - 3.5f;
                sprites[FillCornerSprite(2)].x = drawPos.x + drawSize.x - 3.5f;
                sprites[FillCornerSprite(2)].y = drawPos.y + drawSize.y - 3.5f;
                sprites[FillCornerSprite(3)].x = drawPos.x + drawSize.x - 3.5f;
                sprites[FillCornerSprite(3)].y = drawPos.y + 3.5f;
                sprites[MainFillSprite].x = drawPos.x + 7f;
                sprites[MainFillSprite].y = drawPos.y + 7f;
                sprites[MainFillSprite].scaleX = drawSize.x - 14f;
                sprites[MainFillSprite].scaleY = drawSize.y - 14f;
                for (int j = 0; j < 9; j++)
                {
                    sprites[j].alpha = fillAlpha;
                }
            }
        }

        public void Unload()
        {
            foreach (var sprite in sprites)
            {
                sprite.RemoveFromContainer();
            }
        }
    }
}
