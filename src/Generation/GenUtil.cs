using RWCustom;
using UnityEngine;

namespace MapExporterNew.Generation
{
    internal static class GenUtil
    {
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
