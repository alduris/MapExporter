using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace MapExporterNew.Generation
{
    internal class VistaProcessor(Generator owner) : Processor(owner)
    {
        public override string ProcessName => "Vistas";

        protected override IEnumerator<float> Process()
        {
            var metadata = owner.metadata;
            var regionInfo = owner.regionInfo;

            List<VistaData> data = [];
            foreach (var kv in regionInfo.vistaPoints)
            {
                if (regionInfo.rooms.ContainsKey(kv.Key))
                {
                    data.Add(new VistaData
                    {
                        pos = regionInfo.rooms[kv.Key].devPos + kv.Value,
                        room = kv.Key,
                    });
                }
            }

            metadata["vista_features"] = new JArray(data);

            yield return 1f;
        }

        private struct VistaData : IGeoJsonObject
        {
            public string room;
            public Vector2 pos;

            public readonly JObject Geometry()
            {
                return new JObject()
                {
                    ["type"] = "Point",
                    ["coordinates"] = Vector2ToArray(pos)
                };
            }

            public readonly JObject Properties()
            {
                return new JObject()
                {
                    ["room"] = room
                };
            }
        }
    }
}
