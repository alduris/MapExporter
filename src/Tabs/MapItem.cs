using System.Collections.Generic;
using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace MapExporter.Tabs
{
    internal partial class OpMapBox
    {
        public class MapItem
        {
            public readonly DyeableRect rect;
            public readonly List<Vector2> entrances;
            public readonly HashSet<MapLine> lines;

            public Vector2 pos;
            public Vector2 size;

            public MapItem(FContainer parent, Vector2 pos, Vector2 size, int[][] geometry)
            {
                this.pos = pos;
                this.size = size;
                rect = new DyeableRect(parent, pos, size, true);
                lines = [];

                // something with geo here
            }

            public void Draw()
            {
                rect.pos = pos;
                foreach (var line in lines)
                {
                    line.Draw();
                }
            }

            public void Remove()
            {
                foreach (var line in lines)
                {
                    line.Remove();
                }
            }
        }

        public class MapLine
        {
            public readonly MapItem startBox;
            public int startEntrance;
            public readonly MapItem endBox;
            public int endEntrance;

            public readonly FContainer container;
            public readonly FSprite[] sprites;

            public MapLine(FContainer parent, MapItem startBox, int startEntrance, MapItem endBox, int endEntrance)
            {
                this.startBox = startBox;
                this.endBox = endBox;

                sprites = new FSprite[3];
                sprites[0] = new FSprite("pixel");
                sprites[1] = new FSprite("Circle20") { scale = 0.5f };
                sprites[2] = new FSprite("Circle20") { scale = 0.5f };

                container = new FContainer();
                parent.AddChild(container);
                for (int i = 0; i < sprites.Length; i++)
                {
                    container.AddChild(sprites[i]);
                }
            }

            public void Draw()
            {
                var startPos = startBox.pos + startBox.entrances[startEntrance];
                var endPos = endBox.pos + endBox.entrances[endEntrance];

                sprites[1].SetPosition(startPos);
                sprites[2].SetPosition(endPos);

                sprites[0].SetPosition((startPos + endPos) / 2f);
                sprites[0].scaleX = 2f;
                sprites[0].scaleY = Vector2.Distance(startPos, endPos);
                sprites[0].rotation = Custom.AimFromOneVectorToAnother(startPos, endPos);
            }

            public void Remove()
            {
                startBox.lines.Remove(this);
                endBox.lines.Remove(this);
                foreach (var sprite in sprites)
                {
                    sprite.RemoveFromContainer();
                }
                container.RemoveFromContainer();
            }
        }
    }
}
