using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MapExporter.Generation.GenStructures;

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
    }
}
