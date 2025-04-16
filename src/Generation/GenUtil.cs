﻿using RWCustom;
using UnityEngine;

namespace MapExporterNew.Generation
{
    internal static class GenUtil
    {
        public static IntVector2 Vec2IntVecFloor(Vector2 v) => new(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static IntVector2 Vec2IntVecCeil(Vector2 v) => new(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
        public static float[] Vec2arr(Vector2 vec) => [vec.x, vec.y];
        public static int[] Color2Arr(Color vec) => [(int)(vec.r * 255), (int)(vec.g * 255), (int)(vec.b * 255)];
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
        public static readonly IntVector2 screenSizeInt = new(1366, 768);
        public static readonly Vector2 screenSize = screenSizeInt.ToVector2();
        public static readonly Vector2 camOffset = new(17, 18); // don't ask, this was in the original

        public static readonly Vector2[] fourDirections = [Vector2.right, Vector2.up, Vector2.left, Vector2.down];
    }
}
