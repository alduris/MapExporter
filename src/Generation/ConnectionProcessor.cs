using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static MapExporter.Generation.GenStructures;

namespace MapExporter.Generation
{
    internal class ConnectionProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => throw new NotImplementedException();

        protected override IEnumerator Process()
        {
            // Find room connections
            var regionInfo = owner.regionInfo;
            List<ConnectionInfo> connections = [];
            foreach (var conn in regionInfo.connections)
            {
                connections.Add(new ConnectionInfo
                {
                    pointA = regionInfo.rooms[conn.roomA].devPos + conn.posA.ToVector2() * 20f + Vector2.one * 10f,
                    pointB = regionInfo.rooms[conn.roomB].devPos + conn.posB.ToVector2() * 20f + Vector2.one * 10f,
                    dirA = conn.dirA,
                    dirB = conn.dirB
                });
            }

            owner.metadata["connection_features"] = connections;
            Progress = 1f;
            yield return null;
            Done = true;
        }
    }
}
