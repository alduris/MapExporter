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
                if (room.nodes == null) continue;
                foreach (var data in room.spawns)
                {
                    spawns.Add(new SpawnInfo
                    {
                        roomName = room.roomName,
                        spawnData = data,
                        coords = room.devPos + room.nodes[data[0].den].ToVector2() * 20f + new Vector2(10f, 10f)
                    });
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
