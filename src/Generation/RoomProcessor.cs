using System.Collections.Generic;
using UnityEngine;
using static MapExporter.Generation.GenUtil;

namespace MapExporter.Generation
{
    internal class RoomProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Room outlines";

        protected override IEnumerator<float> Process()
        {
            var regionInfo = owner.regionInfo;
            List<RoomBoxInfo> boxes = [];
            int i = 0;
            foreach (var room in regionInfo.rooms.Values)
            {
                Rect borderRect;
                Vector2 namePos;
                if (room.cameras == null || room.cameras.Length == 0)
                {
                    borderRect = new Rect(room.devPos, offscreenSize);
                    namePos = room.devPos + offscreenSize + Vector2.left * (offscreenSize.x / 2f);
                }
                else
                {
                    Vector2 blPos = room.devPos + room.cameras[0] + camOffset;
                    Vector2 trPos = room.devPos + room.cameras[0] + camOffset + screenSize;
                    for (int j = 1; j < room.cameras.Length; j++)
                    {
                        var cam = room.devPos + room.cameras[j];
                        blPos = new Vector2(Mathf.Min(blPos.x, cam.x), Mathf.Min(blPos.y, cam.y));
                        trPos = new Vector2(Mathf.Max(trPos.x, cam.x + screenSize.x), Mathf.Max(trPos.y, cam.y + screenSize.y));
                    }
                    borderRect = new Rect(blPos, trPos - blPos);
                    namePos = trPos + Vector2.left * ((trPos.x - blPos.x) / 2f);
                }
                boxes.Add(new RoomBoxInfo
                {
                    name = room.roomName,
                    box = borderRect,
                    namePos = namePos
                });

                i++;
                // yield return (float)i / regionInfo.rooms.Count;
            }

            owner.metadata["room_features"] = boxes;
            yield return 1f;
            yield break;
        }

        private struct RoomBoxInfo : IJsonObject
        {
            public string name;
            public Rect box;
            public Vector2 namePos;

            public readonly Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "Polygon" },
                            { "coordinates", new float[][][] { Rect2Arr(box) } }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "name", name },
                            { "popupcoords", Vec2arr(namePos) },
                        }
                    }
                };
            }
        }
    }
}
