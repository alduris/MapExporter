using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MapExporter.RegionInfo.RoomEntry;
using static MapExporter.Generation.GenUtil;

namespace MapExporter.Generation
{
    internal static class GenStructures
    {
        public struct RoomBoxInfo : IJsonObject
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

        public struct ConnectionInfo : IJsonObject
        {
            public Vector2 pointA;
            public int dirA;
            public Vector2 pointB;
            public int dirB;

            private static readonly Vector2[] fourDirections = [Vector2.left, Vector2.down, Vector2.right, Vector2.up];
            public readonly Dictionary<string, object> ToJson()
            {
                float dist = (pointB - pointA).magnitude;
                Vector2 handleA = pointA + fourDirections[dirA] * dist;
                Vector2 handleB = pointB + fourDirections[dirB] * dist;
                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "LineString" },
                            { "coordinates", new float[][] { Vec2arr(pointA), Vec2arr(handleA), Vec2arr(handleB), Vec2arr(pointB) } }
                        }
                    },
                    { "properties", new Dictionary<string, object> {} }
                };
            }
        }

        public struct GeometryInfo : IJsonObject
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

        public struct SpawnInfo : IJsonObject
        {
            public Vector2 coords;
            public string roomName;
            public DenSpawnData[] spawnData;

            public readonly Dictionary<string, object> ToJson()
            {
                // Put together part of the dictionary
                bool isLineage = spawnData[0].chance >= 0f;
                var spawnDict = new Dictionary<string, object>()
                {
                    { "is_lineage", isLineage },
                    { "amount", spawnData[0].count },
                    { "creature", spawnData[0].type },
                    { "spawn_data", spawnData[0].data },
                    { "pre_cycle", false }, // TODO: remove these
                    { "night", spawnData[0].night }
                };

                // Lineage has extra data
                if (isLineage)
                {
                    spawnDict["lineage"] = spawnData.Select(x => x.type).ToArray();
                    spawnDict["lineage_probs"] = spawnData.Select(x => x.chance.ToString("0.0000")).ToArray();
                    spawnDict["lineage_data"] = spawnData.Select(x => x.data).ToArray();
                }

                return new Dictionary<string, object>()
                {
                    { "type", "Feature" },
                    {
                        "geometry",
                        new Dictionary<string, object>
                        {
                            { "type", "Point" },
                            { "coordinates", Vec2arr(coords) }
                        }
                    },
                    {
                        "properties",
                        new Dictionary<string, object>
                        {
                            { "room", roomName },
                            { "den", spawnData[0].den },
                            { "spawns",  spawnDict }
                        }
                    }
                };
            }
        }
    }
}
