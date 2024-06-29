using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static MapExporter.Generation.GenUtil;
using static MapExporter.RegionInfo.RoomEntry;

namespace MapExporter.Generation
{
    internal class SpawnProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Spawns";

        protected override IEnumerator Process()
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
                            coords = room.devPos + (room.nodes == null ? Vector2.zero : room.nodes[data.Key].ToVector2() * 20f + new Vector2(10f, 10f))
                        });
                    }
                    catch (IndexOutOfRangeException)
                    {
                        // ignore :3
                    }
                }
            }

            owner.metadata["spawn_features"] = spawns;

            Progress = 1f;
            yield return null;
            Done = true;
        }

        public struct SpawnInfo : IJsonObject
        {
            public Vector2 coords;
            public int den;
            public string roomName;
            public DenSpawnData[][] spawnData;

            public readonly Dictionary<string, object> ToJson()
            {
                // Put together part of the dictionary
                List<Dictionary<string, object>> spawnDicts = [];
                foreach (var data in spawnData)
                {
                    bool isLineage = data[0].chance >= 0f;
                    var spawnDict = new Dictionary<string, object>()
                    {
                        { "is_lineage", isLineage },
                        { "amount", data[0].count },
                        { "creature", data[0].type },
                        { "spawn_data", data[0].data },
                        { "pre_cycle", false }, // TODO: remove these
                        { "night", data[0].night }
                    };

                    // Lineage has extra data
                    if (isLineage)
                    {
                        spawnDict["lineage"] = data.Select(x => x.type).ToArray();
                        spawnDict["lineage_probs"] = data.Select(x => x.chance.ToString("0.0000")).ToArray();
                        spawnDict["lineage_data"] = data.Select(x => x.data).ToArray();
                    }

                    spawnDicts.Add(spawnDict);
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
                            { "den", den },
                            { "spawns",  spawnDicts }
                        }
                    }
                };
            }
        }
    }
}
