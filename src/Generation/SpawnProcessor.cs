﻿using System;
using System.Collections.Generic;
using System.Linq;
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

            owner.metadata["spawn_features"] = spawns;

            yield return 1f;
            yield break;
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
                            { "coordinates", Vector2ToArray(coords) }
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
