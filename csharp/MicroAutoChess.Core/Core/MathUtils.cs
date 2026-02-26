using System;

namespace MicroAutoChess.Core
{
    public static class MathUtils
    {
        public static bool FloatLessThanOrEqual(double a, double b, double tol = 1e-9)
        {
            return a < b || Math.Abs(a - b) <= tol;
        }
    }
}
