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
            List<PlacedObjectInfo> placedObjects = [];
            List<PlacedObjectInfo> rippleSpawnEggs = [];
            List<WarpPointInfo> warpPoints = [];
            List<WarpDestinationInfo> warpDestinations = [];

            foreach (var room in owner.regionInfo.rooms.Values)
            {
                foreach (var thing in room.placedObjects)
                {
                    if (thing.type == nameof(PlacedObject.Type.WarpPoint))
                    {
                        warpPoints.Add(new WarpPointInfo
                        {
                            roomName = room.roomName,
                            pos = room.devPos + thing.pos,
                            destRegion = thing.data[4] == "NULL" ? null : thing.data[4],
                            destRoom = thing.data[5] == "NULL" ? null : thing.data[5],
                        });
                    }
                    else if (thing.type == nameof(PlacedObject.Type.DynamicWarpTarget))
                    {
                        warpDestinations.Add(new WarpDestinationInfo
                        {
                            roomName = room.roomName,
                            pos = room.devPos + thing.pos,
                            deadEnd = thing.data[3] == "true",
                            badWarp = thing.data[4] == "true",
                            rippleReq = float.TryParse(thing.data[2], out float rippleReq) ? rippleReq : 0f,
                        });
                    }
                    else if (ModManager.Watcher && thing.type == nameof(WatcherEnums.PlacedObjectType.SpinningTopSpot))
                    {
                        warpPoints.Add(new WarpPointInfo
                        {
                            roomName = room.roomName,
                            pos = room.devPos + thing.pos,
                            destRegion = thing.data[3] == "NULL" ? null : thing.data[3],
                            destRoom = thing.data[4] == "NULL" ? null : thing.data[4],
                        });
                        placedObjects.Add(new PlacedObjectInfo
                        {
                            roomName = room.roomName,
                            type = thing.type,
                            pos = room.devPos + thing.pos,
                            settings = thing.data
                        });
                    }
                    else if (thing.type == nameof(PlacedObject.Type.RippleSpawnEgg))
                    {
                        rippleSpawnEggs.Add(new PlacedObjectInfo
                        {
                            roomName = room.roomName,
                            type = thing.type,
                            pos = room.devPos + thing.pos,
                            settings = thing.data
                        });
                    }
                    else
                    {
                        placedObjects.Add(new PlacedObjectInfo
                        {
                            roomName = room.roomName,
                            type = thing.type,
                            pos = room.devPos + thing.pos,
                            settings = thing.data
                        });
                    }
                }
            }

            owner.metadata["placedobject_features"] = placedObjects;
            owner.metadata["ripplespawnegg_features"] = rippleSpawnEggs;
            owner.metadata["warppoint_features"] = warpPoints;
            owner.metadata["warpdest_features"] = warpDestinations;

            yield return 1f;
            yield break;
        }

        public struct PlacedObjectInfo : IJsonObject
        {
            public string roomName;
            public string type;
            public Vector2 pos;
            public List<string> settings;

            public readonly Dictionary<string, object> ToJson()
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

            public readonly Dictionary<string, object> ToJson()
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

        public struct WarpDestinationInfo : IJsonObject
        {
            public string roomName;
            public Vector2 pos;
            public bool deadEnd;
            public bool badWarp;
            public float rippleReq;

            public Dictionary<string, object> ToJson()
            {
                return new Dictionary<string, object>
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
                            { "deadEnd", deadEnd },
                            { "badWarp", badWarp },
                            { "rippleReq", rippleReq }
                        }
                    }
                };
            }
        }
    }
}
