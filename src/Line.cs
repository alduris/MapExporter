using System.Collections.Generic;
using System.Runtime.CompilerServices;
using RWCustom;
using UnityEngine;

namespace MapExporterNew
{
    public struct Line(Vector2 start, Vector2 end)
    {
        public Vector2 start = start;
        public Vector2 end = end;

        public readonly Vector2 GetPoint(float f) => Vector2.LerpUnclamped(start, end, f);

        public readonly Line CutAt(float f, bool right) => right ? new Line(GetPoint(f), end) : new Line(start, GetPoint(f));

        public readonly float? CollisionWith(Line other)
        {
            Vector2 r = end - start;
            Vector2 s = other.end - other.start;
            float t = Cross2D(other.start - start, s / Cross2D(r, s));

            if (t > 0 && t < 1)
            {
                return t;
            }
            return null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static float Cross2D(Vector2 a, Vector2 b)
            {
                return a.x * b.y - a.y * b.x;
            }
        }

        public readonly IEnumerable<float> CollisionWith(BezierCurve curve)
        {
            float len = curve.Length;
            int iterations = (int)(len / 5);
            float diff = 1f / iterations;
            for (int i = 0; i < iterations; i++)
            {
                float t = Mathf.InverseLerp(0f, iterations, i);
                if (CollisionWith(new Line(curve.GetPoint(t), curve.GetPoint(t + diff))) is { } f)
                {
                    yield return f;
                }
            }
        }

        public override readonly int GetHashCode()
        {
            return start.GetHashCode() ^ end.GetHashCode();
        }

        public override readonly bool Equals(object obj)
        {
            return obj is Line line && ((line.start == start && line.end == end) || (line.start == end && line.end == start));
        }

        public static Line operator +(Line line, Vector2 pos)
        {
            return new Line(line.start + pos, line.end + pos);
        }

        public static Line operator -(Line line, Vector2 pos)
        {
            return new Line(line.start - pos, line.end - pos);
        }
    }
}
