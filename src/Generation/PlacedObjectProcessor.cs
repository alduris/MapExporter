using System.Collections.Generic;
using UnityEngine;
using static MapExporterNew.Generation.GenUtil;

namespace MapExporterNew.Generation
{
    internal class PlacedObjectProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Placed objects";

        protected override IEnumerator<float> Process()
        {
            List<PlacedObjectInfo> POs = [];

            foreach (var room in owner.regionInfo.rooms.Values)
            {
                foreach (var thing in room.placedObjects)
                {
                    POs.Add(new PlacedObjectInfo
                    {
                        roomName = room.roomName,
                        type = thing.type,
                        pos = room.devPos + thing.pos,
                        settings = thing.data
                    });
                }
            }

            owner.metadata["placedobject_features"] = POs;

            yield return 1f;
            yield break;
        }

        public struct PlacedObjectInfo : IJsonObject
        {
            public string roomName;
            public string type;
            public Vector2 pos;
            public List<string> settings;
            public Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "Point" },
                            { "coordinates", Vec2arr(pos) }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "room", roomName },
                            { "object", type },
                            { "settings", settings }
                        }
                    }
                };
            }
        }
    }
}
