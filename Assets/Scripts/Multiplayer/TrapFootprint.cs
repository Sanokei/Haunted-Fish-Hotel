using System;
namespace HauntedFish.Multiplayer
{
    // Floor footprints reserve an aisle column, including a ceiling trap's landing.
    public readonly struct TrapFootprint
    {
        public readonly double X, Z, HalfX, HalfZ, Cos, Sin;
        public TrapFootprint(double x, double z, double halfX, double halfZ, double degrees)
        {
            X = x; Z = z; HalfX = Math.Abs(halfX); HalfZ = Math.Abs(halfZ);
            double angle = degrees * Math.PI / 180;
            Cos = Math.Cos(angle); Sin = Math.Sin(angle);
        }
        public bool Overlaps(TrapFootprint other)
        {
            return IntersectsAxis(other, Cos, -Sin) && IntersectsAxis(other, Sin, Cos) &&
                IntersectsAxis(other, other.Cos, -other.Sin) && IntersectsAxis(other, other.Sin, other.Cos);
        }
        bool IntersectsAxis(TrapFootprint other, double x, double z)
        {
            double distance = Math.Abs((other.X - X) * x + (other.Z - Z) * z);
            double extent = HalfX * Math.Abs(Cos * x - Sin * z) + HalfZ * Math.Abs(Sin * x + Cos * z) +
                other.HalfX * Math.Abs(other.Cos * x - other.Sin * z) + other.HalfZ * Math.Abs(other.Sin * x + other.Cos * z);
            // Shared edges are legal; tolerate only floating point contact noise.
            return distance < extent - .0001;
        }
    }
}