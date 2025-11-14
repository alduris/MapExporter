using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MapExporterNew;

interface IGeoJsonObject
{
    JObject Geometry();
    JObject Properties();

    public class GeoJsonConverter : JsonConverter<IGeoJsonObject>
    {
        public override bool CanRead => false;

        public override IGeoJsonObject ReadJson(JsonReader reader, Type objectType, IGeoJsonObject existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            // We do not specify 
            throw new InvalidOperationException("IJsonObject is not intended to be read!");
        }

        public override void WriteJson(JsonWriter writer, IGeoJsonObject value, JsonSerializer serializer)
        {
            var o = new JObject()
            {
                ["type"] = "Feature",
                ["geometry"] = value.Geometry(),
                ["properties"] = value.Properties()
            };
            o.WriteTo(writer);
        }
    }
}
