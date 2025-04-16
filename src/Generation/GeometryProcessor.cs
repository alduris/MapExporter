using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MapExporterNew.Generation.GenUtil;

namespace MapExporterNew.Generation
{
    internal class GeometryProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Room geometry";

        protected override IEnumerator<float> Process()
        {
            var regionInfo = owner.regionInfo;
            List<GeometryInfo> geo = [];
            int processed = 0;
            foreach (var room in regionInfo.rooms.Values)
            {
                // I think the code for processing geo changed the most from how the Python file did it, I pretty much rewrote it from scratch lol
                if (room.size == default || room.size.x == 0 || room.size.y == 0) continue;

                LinkedList<(Vector2 A, Vector2 B)> lines = [];

                // Create the lines
                for (int j = 0; j < room.size.y; j++)
                {
                    float y = room.devPos.y + j * 20f;
                    for (int i = 0; i < room.size.x; i++)
                    {
                        float x = room.devPos.x + i * 20f;
                        int[] tile = room.tiles[i, j];
                        int type = tile[0];
                        bool hpole = ((tile[1] & 1) != 0), vpole = ((tile[1] & 2) != 0);

                        // Tile type
                        switch (type)
                        {
                            case 0 or 1: // air and solid, respectively
                                if (i != room.size.x - 1)
                                {
                                    int neighbor = room.tiles[i + 1, j][0];
                                    if (type != neighbor && (neighbor & -2) == 0) // number & -2 will return 0 for either 0 or 1
                                    {
                                        lines.AddLast((new(x + 20f, y), new(x + 20f, y + 20f)));
                                    }
                                }
                                if (j != room.size.y - 1)
                                {
                                    int neighbor = room.tiles[i, j + 1][0];
                                    if (type != neighbor && (neighbor & -2) == 0)
                                    {
                                        lines.AddLast((new(x, y + 20f), new(x + 20f, y + 20f)));
                                    }
                                }
                                break;
                            case 2: // slopes
                                // Need to check all four orientations, but there are only two cases of lines to draw.
                                // In this, we are considering any check outside of the bounds of the room geometry to be solid.
                                bool up = j == room.size.y - 1 || room.tiles[i, j + 1][0] == 1;
                                bool down = j == 0 || room.tiles[i, j - 1][0] == 1;
                                bool right = i == room.size.x - 1 || room.tiles[i + 1, j][0] == 1;
                                bool left = i == 0 || room.tiles[i - 1, j][0] == 1;

                                if (up == left && down == right)
                                {
                                    lines.AddLast((new(x, y), new(x + 20f, y + 20f)));
                                }
                                else if (up == right && down == left)
                                {
                                    lines.AddLast((new(x, y + 20f), new(x + 20f, y)));
                                }
                                break;
                            case 3: // half floors
                                    // Top and bottom lines always get drawn
                                lines.AddLast((new(x, y + 20f), new(x + 20f, y + 20f)));
                                lines.AddLast((new(x, y + 10f), new(x + 20f, y + 10f)));

                                // The edges get a little funky.
                                // We don't draw if there is another half floor there but otherwise a line needs to be drawn *somewhere*.
                                int l = i == 0 ? 3 : room.tiles[i - 1, j][0];
                                int r = i == room.size.x - 1 ? 3 : room.tiles[i + 1, j][0];
                                if (l != 3)
                                {
                                    float o = l == 1 ? 0f : 10f;
                                    lines.AddLast((new(x, y + o), new(x, y + o + 10f)));
                                }
                                if (r != 3)
                                {
                                    float o = l == 1 ? 0f : 10f;
                                    lines.AddLast((new(x + 20f, y + o), new(x + 20f, y + o + 10f)));
                                }
                                break;
                            default: // anything else
                                break;
                        }

                        // Poles
                        if (type != 1) // don't draw poles if solid
                        {
                            if (hpole)
                                lines.AddLast((new(x, y + 10f), new(x + 20f, y + 10f)));
                            if (vpole)
                                lines.AddLast((new(x + 10f, y), new(x + 10f, y + 20f)));
                        }
                    }
                }

                // Optimize the lines (combining)
                List<LinkedList<Vector2>> optimized = [];
                HashSet<(Vector2, Vector2)> seen = [];

                var node = lines.First; // the type for this is very long lol
                while (node != null)
                {
                    // Don't add duplicate elements
                    if (!seen.Add(node.Value))
                    {
                        node = node.Next;
                        continue;
                    }

                    // Try to create a continuous line segment as long as possible
                    LinkedList<Vector2> line = new([node.Value.A, node.Value.B]);

                    var curr = node.Next;
                    while (curr != null)
                    {
                        var next = curr.Next;
                        bool remove = true;

                        if (line.First.Value == curr.Value.A)
                        {
                            line.AddFirst(curr.Value.B);
                        }
                        else if (line.First.Value == curr.Value.B)
                        {
                            line.AddFirst(curr.Value.A);
                        }
                        else if (line.Last.Value == curr.Value.A)
                        {
                            line.AddLast(curr.Value.B);
                        }
                        else if (line.Last.Value == curr.Value.B)
                        {
                            line.AddLast(curr.Value.A);
                        }
                        else
                        {
                            remove = false;
                        }

                        if (remove)
                        {
                            lines.Remove(curr);
                        }

                        curr = next;
                    }

                    optimized.Add(line);
                    node = node.Next;
                }

                geo.Add(new GeometryInfo
                {
                    room = room.roomName,
                    lines = optimized.Select(x => x.Select(Vec2arr).ToArray()).ToArray(),
                });

                processed++;
                if (owner.lessResourceIntensive || processed % 20 == 0)
                {
                    yield return (float)processed / regionInfo.rooms.Count;
                }
            }

            owner.metadata["geo_features"] = geo;
            
            yield break;
        }

        private struct GeometryInfo : IJsonObject
        {
            public string room;
            public float[][][] lines;

            public readonly Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "MultiLineString" },
                            { "coordinates", lines }
                        }
                    },
                    { "properties", new Dictionary<string, object> { { "room", room } } }
                };
            }
        }
    }
}
