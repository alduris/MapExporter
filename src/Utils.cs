global using static MapExporterNew.Utils;
using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MapExporterNew
{
    public static class Utils
    {
        public static Dictionary<string, object> BezierToJson(BezierCurve curve)
        {
            return new Dictionary<string, object>
            {
                ["posA"] = Vector2ToArray(curve.posA),
                ["posB"] = Vector2ToArray(curve.posB),
                ["handleA"] = Vector2ToArray(curve.handleA),
                ["handleB"] = Vector2ToArray(curve.handleB)
            };
        }
        public static BezierCurve BezierFromJson(Dictionary<string, object> json)
        {
            return new BezierCurve(Vector2FromJson(json["posA"]), Vector2FromJson(json["handleA"]), Vector2FromJson(json["posB"]), Vector2FromJson(json["handleB"]));
        }

        public static IntVector2 Vector2FloorToIntVector(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static IntVector2 Vector2CeilToIntVector(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));

        public static float[] Vector2ToArray(Vector2 vec) => [vec.x, vec.y];
        public static Vector2 Vector2FromArray(float[] vec) => new(vec[0], vec[1]);
        public static Vector2 Vector2FromList(List<float> vec) => new(vec[0], vec[1]);
        public static Vector2 Vector2FromList(List<object> vec) => new((float)(double)vec[0], (float)(double)vec[1]);
        public static Vector2 Vector2FromJson(object vec) => Vector2FromList((List<object>)vec);

        public static float[] Vector3ToArray(Vector3 vec) => [vec.x, vec.y, vec.z];
        
        public static int[] IntVectorToArray(IntVector2 vec) => [vec.x, vec.y];
        public static IntVector2 IntVectorFromList(List<object> vec) => new((int)(long)vec[0], (int)(long)vec[1]);
        public static IntVector2 IntVectorFromJson(object vec) => IntVectorFromList((List<object>)vec);
        
        public static int[] ColorToArray(Color vec) => [(int)(vec.r * 255), (int)(vec.g * 255), (int)(vec.b * 255)];
        public static Color ColorFromArray(List<object> arr) => new((float)(double)arr[0], (float)(double)arr[1], (float)(double)arr[2]);

        public static float[][] RectToArray(Rect rect) => [
                Vector2ToArray(new Vector2(rect.xMin, rect.yMin)),
                Vector2ToArray(new Vector2(rect.xMin, rect.yMax)),
                Vector2ToArray(new Vector2(rect.xMax, rect.yMax)),
                Vector2ToArray(new Vector2(rect.xMax, rect.yMin)),
                Vector2ToArray(new Vector2(rect.xMin, rect.yMin))
            ];
    }
}
