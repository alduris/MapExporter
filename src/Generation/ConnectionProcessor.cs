﻿using System.Collections.Generic;
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

            owner.metadata["connection_features"] = connections;
            yield return 1f;
            yield break;
        }

        private struct ConnectionInfo : IJsonObject
        {
            public Vector2 pointA;
            public int dirA;
            public Vector2 pointB;
            public int dirB;

            public readonly Dictionary<string, object> ToJson()
            {
                float dist = (pointB - pointA).magnitude / 4f;
                Vector2 basicDir = (pointB - pointA).normalized;
                Vector2 handleA = pointA + (dirA == -1 ? basicDir : fourDirections[dirA]) * dist;
                Vector2 handleB = pointB + (dirB == -1 ? -basicDir : fourDirections[dirB]) * dist;
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
    }
}
