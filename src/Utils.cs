global using static MapExporterNew.Utils;
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RWCustom;
using UnityEngine;

namespace MapExporterNew
{
    public static class Utils
    {
        public static void Deconstruct<T1, T2>(this KeyValuePair<T1, T2> tuple, out T1 key, out T2 value)
        {
            key = tuple.Key;
            value = tuple.Value;
        }

        public static IntVector2 Vector2FloorToIntVector(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static IntVector2 Vector2CeilToIntVector(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));

        public static JArray Vector2ToArray(Vector2 vec) => [vec.x, vec.y];
        public static JArray Vector3ToArray(Vector3 vec) => [vec.x, vec.y, vec.z];
        public static JArray ColorToArray(Color vec) => [(int)(vec.r * 255), (int)(vec.g * 255), (int)(vec.b * 255)];
        public static JArray RectToArray(Rect rect) => [
                Vector2ToArray(new Vector2(rect.xMin, rect.yMin)),
                Vector2ToArray(new Vector2(rect.xMin, rect.yMax)),
                Vector2ToArray(new Vector2(rect.xMax, rect.yMax)),
                Vector2ToArray(new Vector2(rect.xMax, rect.yMin)),
                Vector2ToArray(new Vector2(rect.xMin, rect.yMin))
            ];

        public class UnityStructConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                return objectType.Equals(typeof(Vector2))
                    || objectType.Equals(typeof(Vector3))
                    || objectType.Equals(typeof(Color))
                    || objectType.Equals(typeof(Rect));
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                // Needs to be a 
                if (reader.TokenType != JsonToken.StartArray) return null;
                var arr = JArray.Load(reader);

                if (typeof(Vector2).Equals(objectType))
                {
                    return new Vector2((float)arr[0], (float)arr[1]);
                }
                else if (typeof(Vector3).Equals(objectType))
                {
                    return new Vector3((float)arr[0], (float)arr[1], (float)arr[2]);
                }
                else if (typeof(Color).Equals(objectType))
                {
                    return new Color((float)arr[0], (float)arr[1], (float)arr[2]);
                }
                else if (typeof(Rect).Equals(objectType))
                {
                    var minArr = (JArray)arr[0];
                    var maxArr = (JArray)arr[2];
                    var minVec = new Vector2((float)minArr[0], (float)minArr[1]);
                    var maxVec = new Vector2((float)maxArr[0], (float)maxArr[1]);
                    return Rect.MinMaxRect(minVec.x, minVec.y, maxVec.x, maxVec.y);
                }
                else
                {
                    return null;
                }
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                switch (value)
                {
                    case Vector2 v2: Vector2ToArray(v2).WriteTo(writer); break;
                    case Vector3 v3: Vector3ToArray(v3).WriteTo(writer); break;
                    case Color c: ColorToArray(c).WriteTo(writer); break;
                    case Rect r: RectToArray(r).WriteTo(writer); break;
                }
            }
        }
    }
}
