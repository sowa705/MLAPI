namespace MLAPI.Engine
{
    public struct Quaternion
    {
        private const float SlerpEpsilon = 1e-6f;

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float W { get; set; }

        public static Quaternion Identity => new Quaternion(0, 0, 0, 1);

        public Quaternion(float x, float y, float z, float w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public static Quaternion Euler(float x, float y, float z)
        {
            //  Roll first, about axis the object is facing, then
            //  pitch upward, then yaw to face into the new heading
            float sr, cr, sp, cp, sy, cy;

            float halfRoll = z * 0.5f * MathF.Deg2Rad;
            sr = MathF.Sin(halfRoll);
            cr = MathF.Cos(halfRoll);

            float halfPitch = y * 0.5f * MathF.Deg2Rad;
            sp = MathF.Sin(halfPitch);
            cp = MathF.Cos(halfPitch);

            float halfYaw = x * 0.5f * MathF.Deg2Rad;
            sy = MathF.Sin(halfYaw);
            cy = MathF.Cos(halfYaw);


            return new Quaternion(cy * sp * cr + sy * cp * sr, 
                                  sy * cp * cr - cy * sp * sr, 
                                  cy * cp * sr - sy * sp * cr, 
                                  cy * cp * cr + sy * sp * sr);
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            float cosinOmega = a.X * b.X + a.Y * b.Y +
                             a.Z * b.Z + a.W * b.W;

            bool flip = false;

            if (cosinOmega < 0.0f)
            {
                flip = true;
                cosinOmega = -cosinOmega;
            }

            float s1, s2;

            if (cosinOmega > (1.0f - SlerpEpsilon))
            {
                // Too close, do straight linear interpolation.
                s1 = 1.0f - t;
                s2 = (flip) ? -t : t;
            }
            else
            {
                float omega = MathF.Acos(cosinOmega);
                float invSinOmega = 1 / MathF.Sin(omega);

                s1 = MathF.Sin((1.0f - t) * omega) * invSinOmega;
                s2 = (flip)
                    ? -MathF.Sin(t * omega) * invSinOmega
                    : MathF.Sin(t * omega) * invSinOmega;
            }

            return new Quaternion(s1 * a.X + s2 * b.X,
                                  s1 * a.Y + s2 * b.Y,
                                  s1 * a.Z + s2 * b.Z,
                                  s1 * a.W + s2 * b.W);
        }
    }
}
