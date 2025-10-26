using System;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod
{
    public static class VectorExtensionMethods
    {
        public static Vec3i ToRoughPlace(
            this Vec3d vec,
            int resolution,
            int offset)
        {
            return new Vec3i(
                (int)Math.Floor(vec.X / resolution) * resolution + offset,
                (int)Math.Floor(vec.Y / resolution) * resolution + offset,
                (int)Math.Floor(vec.Z / resolution) * resolution + offset
            );
        }

        public static Vec2d ToXZ(this Vec3d vec)
        {
            return new Vec2d(vec.X, vec.Z);
        }
    }
}
