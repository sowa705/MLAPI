namespace MLAPI.Engine
{
    public struct Vector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vector3 Zero => new Vector3(0, 0, 0);

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
        {
            if (t < 0) t = 0;
            else if (t > 1) t = 1;

            return LerpUnclamped(a, b, t);
        }

        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t)
        {
            return new Vector3(a.X + (b.X - a.X) * t, 
                               a.Y + (b.Y - a.Y) * t, 
                               a.Z + (b.Z - a.Z) * t);
        }
    }
}
