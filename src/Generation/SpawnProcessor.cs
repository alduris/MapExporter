using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static MapExporterNew.Generation.GenUtil;
using static MapExporterNew.RegionInfo.RoomEntry;

namespace MapExporterNew.Generation
{
    internal class SpawnProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Spawns";

        protected override IEnumerator<float> Process()
        {
            List<SpawnInfo> spawns = [];

            foreach (var room in owner.regionInfo.rooms.Values)
            {

                var dens = room.spawns.GroupBy(x => x[0].den);
                foreach (var data in dens)
                {
                    try
                    {
                        spawns.Add(new SpawnInfo
                        {
                            roomName = room.roomName,
                            den = data.Key,
                            spawnData = [.. data],
                            coords = room.devPos + (room.nodes == null ? (room.offscreenDen ? offscreenSize / 2f : Vector2.zero) : room.nodes[data.Key].ToVector2() * 20f + new Vector2(10f, 10f))
                        });
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // ignore :3
                    }
                }
            }

            owner.metadata["spawn_features"] = new JArray(spawns);

            yield return 1f;
            yield break;
        }

        public struct SpawnInfo : IGeoJsonObject
        {
            public Vector2 coords;
            public int den;
            public string roomName;
            public DenSpawnData[][] spawnData;

            public readonly JObject Geometry()
            {
                return new JObject()
                {
                    ["type"] = "Point",
                    ["coordinates"] = Vector2ToArray(coords)
                };
            }

            public readonly JObject Properties()
            {
                // Put together part of the dictionary
                JArray spawnDicts = [];
                foreach (var data in spawnData)
                {
                    bool isLineage = data[0].chance >= 0f;
                    var spawnDict = new JObject()
                    {
                        { "is_lineage", isLineage },
                        { "amount", data[0].count },
                        { "creature", data[0].type },
                        { "spawn_data", data[0].data },
                    };

                    // Lineage has extra data
                    if (isLineage)
                    {
                        spawnDict["lineage"] = new JArray(data.Select(x => x.type));
                        spawnDict["lineage_probs"] = new JArray(data.Select(x => x.chance));
                        spawnDict["lineage_data"] = new JArray(data.Select(x => x.data));
                    }

                    spawnDicts.Add(spawnDict);
                }

                return new JObject()
                {
                    ["room"] = roomName,
                    ["den"] = den,
                    ["spawns"] = spawnDicts
                };
            }
        }
    }
}
