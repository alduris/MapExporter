using RWCustom;
using Unity.Collections;
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

        public static readonly IntVector2 tileSizeInt = new(256, 256);
        public static readonly Vector2 tileSize = tileSizeInt.ToVector2();
        public static readonly IntVector2 offscreenSizeInt = new(1200, 400);
        public static readonly Vector2 offscreenSize = offscreenSizeInt.ToVector2();
        public static readonly IntVector2 screenSizeInt = new(1400, 800);
        public static readonly Vector2 screenSize = screenSizeInt.ToVector2();

    }
}
