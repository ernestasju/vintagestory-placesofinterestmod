using System;
using Vintagestory.API.MathTools;

namespace PlacesOfInterestMod;

public static class MathExtensions
{
#pragma warning disable S2325 // Methods and properties that don't access instance data should be static

    extension(Vec3d v)
    {
        public Vec3i ToRoughPlace(
            int resolution,
            int offset)
        {
            return new Vec3i(
                ((int)Math.Floor(v.X / resolution) * resolution) + offset,
                ((int)Math.Floor(v.Y / resolution) * resolution) + offset,
                ((int)Math.Floor(v.Z / resolution) * resolution) + offset);
        }

        public Vec2d ToXZ()
        {
            return new Vec2d(v.X, v.Z);
        }
    }

    extension(Vec3i v)
    {
        public Vec3d ToVec3d()
        {
            return new(v.X, v.Y, v.Z);
        }
    }

    extension(Random r)
    {
        public double NextDouble(double from, double to)
        {
            return from + (r.NextDouble() * (to - from));
        }

        public double NextSigned(double value)
        {
            return value * (1 - ((r.Next() & 1) << 1));
        }

        public Vec3d NextCubePoint(double radius)
        {
            return new(
                r.NextDouble(-radius, radius),
                r.NextDouble(-radius, radius),
                r.NextDouble(-radius, radius));
        }

        public Vec3d NextCubeSurfacePoint(double radius)
        {
            return new(
                r.NextSigned(radius),
                r.NextSigned(radius),
                r.NextSigned(radius));
        }

#pragma warning restore S2325 // Methods and properties that don't access instance data should be static
    }
}
