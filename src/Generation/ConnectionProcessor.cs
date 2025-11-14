using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using static MapExporterNew.Generation.GenUtil;

namespace MapExporterNew.Generation
{
    internal class ConnectionProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Connections";

        protected override IEnumerator<float> Process()
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

            owner.metadata["connection_features"] = new JArray(connections);
            yield return 1f;
            yield break;
        }

        private struct ConnectionInfo : IGeoJsonObject
        {
            public Vector2 pointA;
            public int dirA;
            public Vector2 pointB;
            public int dirB;

            public readonly JObject Geometry()
            {
                float dist = (pointB - pointA).magnitude / 4f;
                Vector2 basicDir = (pointB - pointA).normalized;
                Vector2 handleA = pointA + (dirA == -1 ? basicDir : fourDirections[dirA]) * dist;
                Vector2 handleB = pointB + (dirB == -1 ? -basicDir : fourDirections[dirB]) * dist;
                return new JObject()
                {
                    ["type"] = "LineString",
                    ["coordinates"] = new JArray(Vector2ToArray(pointA), Vector2ToArray(handleA), Vector2ToArray(handleB), Vector2ToArray(pointB))
                };
            }

            public readonly JObject Properties()
            {
                return [];
            }
        }
    }
}
