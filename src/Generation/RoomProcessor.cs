using System.Collections.Generic;
using UnityEngine;
using static MapExporterNew.Generation.GenUtil;

namespace MapExporterNew.Generation
{
    internal class RoomProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Room placements";

        protected override IEnumerator<float> Process()
        {
            var regionInfo = owner.regionInfo;
            List<RoomBoxInfo> roomBoxes = [];
            List<RoomTagInfo> roomTags = [];

            int i = 0;
            foreach (var room in regionInfo.rooms.Values)
            {
                // Room boxes
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
                roomBoxes.Add(new RoomBoxInfo
                {
                    name = room.roomName,
                    box = borderRect,
                    namePos = namePos
                });

                // Room tags
                if (room.tags?.Length > 0)
                {
                    roomTags.Add(new RoomTagInfo
                    {
                        name = room.roomName,
                        tags = room.tags,
                        pos = namePos + Vector2.down * 30f
                    });
                }

                i++;
                // yield return (float)i / regionInfo.rooms.Count;
            }

            owner.metadata["room_features"] = roomBoxes;
            owner.metadata["roomtag_features"] = roomTags;
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
                            { "coordinates", new float[][][] { RectToArray(box) } }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "name", name },
                            { "popupcoords", Vector2ToArray(namePos) },
                        }
                    }
                };
            }
        }

        private struct RoomTagInfo : IJsonObject
        {
            public string name;
            public Vector2 pos;
            public string[] tags;

            public Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>()
                        {
                            {"type", "Point" },
                            { "coordinates", Vector2ToArray(pos) }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>()
                        {
                            { "room", name },
                            { "tags", tags }
                        }
                    }
                };
            }
        }
    }
}
