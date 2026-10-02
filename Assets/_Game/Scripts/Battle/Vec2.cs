using System;

namespace MergeLegion.Battle
{
    /// <summary>Ground-plane vector (x, z in world space) for the deterministic battle simulation.</summary>
    public struct Vec2
    {
        public float x;
        public float y;

        public Vec2(float x, float y) { this.x = x; this.y = y; }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.x + b.x, a.y + b.y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.y - b.y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.x * s, a.y * s);

        public float SqrMagnitude => x * x + y * y;
        public float Magnitude => (float)Math.Sqrt(x * x + y * y);

        public static float SqrDistance(Vec2 a, Vec2 b)
        {
            float dx = a.x - b.x, dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        public static float Distance(Vec2 a, Vec2 b) => (float)Math.Sqrt(SqrDistance(a, b));

        public Vec2 Normalized()
        {
            float m = Magnitude;
            return m > 1e-6f ? new Vec2(x / m, y / m) : new Vec2(0f, 0f);
        }
    }
}
