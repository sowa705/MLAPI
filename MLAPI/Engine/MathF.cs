using System;

namespace MLAPI.Engine
{
    internal static class MathF
    {
        internal static float Acos(float f)
        {
            return (float)Math.Acos(f);
        }

        internal static float Sin(float f)
        {
            return (float)Math.Sin(f);
        }

        internal static float Cos(float f)
        {
            return (float)Math.Cos(f);
        }

        internal static float Deg2Rad => (float)(Math.PI / 180);

        internal static bool Approximately(float a, float b)
        {
            return Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), 1e-6f * 8f);
        }

        internal static float Clamp(float value, float min, float max)
        {
            if (value < min) value = min;
            else if (value > max) value = max;

            return value;
        }
    }
}
