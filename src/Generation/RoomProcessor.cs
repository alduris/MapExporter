using System.Collections;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;
using static MapExporter.Generation.GenStructures;

namespace MapExporter.Generation
{
    internal class RoomProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Room outlines";

        private static readonly IntVector2 offscreenSizeInt = new(1200, 400);
        private static readonly Vector2 offscreenSize = offscreenSizeInt.ToVector2();
        private static readonly IntVector2 screenSizeInt = new(1400, 800);
        private static readonly Vector2 screenSize = screenSizeInt.ToVector2();

        protected override IEnumerator Process()
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
                    Vector2 blPos = room.cameras[0];
                    Vector2 trPos = room.cameras[0] + screenSize;
                    for (int j = 1; j < room.cameras.Length; j++)
                    {
                        var cam = room.cameras[j];
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
                Progress = (float)i / regionInfo.rooms.Count;
            }

            owner.metadata["room_features"] = boxes;
            Progress = 1f;
            yield return null;
            Done = true;
        }
    }
}
