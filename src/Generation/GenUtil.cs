using System.Collections.Generic;
using RWCustom;
using UnityEngine;

namespace MapExporter.Generation
{
    internal static class GenUtil
    {
        public static IntVector2 Vec2IntVecFloor(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static IntVector2 Vec2IntVecCeil(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
        public static float[] Vec2arr(Vector2 vec) => [vec.x, vec.y];
        public static float[] Color2Arr(Color vec) => [vec.r, vec.g, vec.b];
        public static float[][] Rect2Arr(Rect rect) => [
                Vec2arr(new Vector2(rect.xMin, rect.yMin)),
                Vec2arr(new Vector2(rect.xMin, rect.yMax)),
                Vec2arr(new Vector2(rect.xMax, rect.yMax)),
                Vec2arr(new Vector2(rect.xMax, rect.yMin)),
                Vec2arr(new Vector2(rect.xMin, rect.yMin))
            ];

        public static Color Mode(List<Color> colors)
        {
            Dictionary<Color, int> map = [];
            int max = 0;
            Color maxColor = Color.black;
            for (int i = 0; i < colors.Count; i++)
            {
                var color = colors[i];
                if (map.ContainsKey(color))
                {
                    map[color]++;
                }
                else
                {
                    map.Add(color, 0);
                }

                if (map[color] > max)
                {
                    max = map[color];
                    maxColor = color;
                }
            }

            return maxColor;
        }

        public static Vector3 HSL2HSV(Vector3 hsl)
        {
            var (h, s, l) = (hsl.x, hsl.y, hsl.z);
            float v = l + s * Mathf.Min(l, 1 - l);
            return new Vector3(h, v == 0f ? 0f : 2 * (1 - l / v), v);
        }

        public static HSLColor HSV2HSL(float h, float s, float v)
        {
            float l = v * (1f - s / 2f);
            return new HSLColor(h, (l == 0 || l == 1) ? 0f : ((v - l) / Mathf.Min(l, 1 - l)), l);
        }
    }
}
