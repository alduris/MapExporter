using System.Collections.Generic;
using UnityEngine;
using Watcher;

namespace MapExporterNew.Generation
{
    internal class PlacedObjectProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Placed objects";

        protected override IEnumerator<float> Process()
        {
            List<PlacedObjectInfo> POs = [];
            List<PlacedObjectInfo> RSEs = [];
            List<WarpPointInfo> WPs = [];

            foreach (var room in owner.regionInfo.rooms.Values)
            {
                foreach (var thing in room.placedObjects)
                {
                    if (thing.type == nameof(PlacedObject.Type.WarpPoint))
                    {
                        WPs.Add(new WarpPointInfo
                        {
                            roomName = room.roomName,
                            pos = room.devPos + thing.pos,
                            destRegion = thing.data[4] == "NULL" ? null : thing.data[4],
                            destRoom = thing.data[5] == "NULL" ? null : thing.data[5],
                        });
                    }
                    else if (ModManager.Watcher && thing.type == nameof(WatcherEnums.PlacedObjectType.SpinningTopSpot))
                    {
                        WPs.Add(new WarpPointInfo
                        {
                            roomName = room.roomName,
                            pos = room.devPos + thing.pos,
                            destRegion = thing.data[3] == "NULL" ? null : thing.data[3],
                            destRoom = thing.data[4] == "NULL" ? null : thing.data[4],
                        });
                        POs.Add(new PlacedObjectInfo
                        {
                            roomName = room.roomName,
                            type = thing.type,
                            pos = room.devPos + thing.pos,
                            settings = thing.data
                        });
                    }
                    else if (thing.type == nameof(PlacedObject.Type.RippleSpawnEgg))
                    {
                        RSEs.Add(new PlacedObjectInfo
                        {
                            roomName = room.roomName,
                            type = thing.type,
                            pos = room.devPos + thing.pos,
                            settings = thing.data
                        });
                    }
                    else
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
            }

            owner.metadata["placedobject_features"] = POs;
            owner.metadata["ripplespawnegg_features"] = RSEs;
            owner.metadata["warppoint_features"] = WPs;

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
                            { "coordinates", Vector2ToArray(pos) }
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

        public struct WarpPointInfo : IJsonObject
        {
            public string roomName;
            public Vector2 pos;
            public string destRegion;
            public string destRoom;

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
                            { "coordinates", Vector2ToArray(pos) }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "room", roomName },
                            { "destRegion", destRegion },
                            { "destRoom", destRoom }
                        }
                    }
                };
            }
        }
    }
}
