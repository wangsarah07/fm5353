using System;
using System.Threading;

// random-number and low-discrepancy sequence generators
public static class RandomGenerators
{
    // creates separate random number generator per thread
    private static readonly ThreadLocal<Random> _threadRng =
        new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

    // generate uniform random between 0 and 1
    private static double U() => _threadRng.Value!.NextDouble();
    // generate uniform random between -1 and 1
    private static double U11() => U() * 2.0 - 1.0;

    // sum of twelve uniforms gives an approximate standard normal variable
    public static double Sum12()
    {
        double sum = 0.0;
        for (int i = 0; i < 12; i++) sum += U();
        return sum - 6.0;
    }

    // box muller generates two independent standard normal variables
    public static (double z1, double z2) BoxMuller()
    {
        double x1;
        do { x1 = U(); } while (x1 <= 0.0);
        double x2 = U();

        double r = Math.Sqrt(-2.0 * Math.Log(x1));
        double angle = 2.0 * Math.PI * x2;
        return (r * Math.Cos(angle), r * Math.Sin(angle));
    }

    // this method is used when the engine uses threading
    public static (double z1, double z2) BoxMullerThreadSafe() => BoxMuller();

    // polar rejection method for generating a pair of normal variables
    public static (double z1, double z2) PolarRejection()
    {
        double x1, x2, w;
        do
        {
            x1 = U11();
            x2 = U11();
            w = x1 * x1 + x2 * x2;
        }
        while (w <= 0.0 || w >= 1.0);

        double c = Math.Sqrt(-2.0 * Math.Log(w) / w);
        return (c * x1, c * x2);
    }

    // approximate standard normal cumulative distribution function
    public static double NormalCDF(double x)
    {
        if (x < 0.0) return 1.0 - NormalCDF(-x);
        const double a1 = 0.319381530, a2 = -0.356563782, a3 = 1.781477937, a4 = -1.821255978, a5 = 1.330274429, p = 0.2316419;
        double t = 1.0 / (1.0 + p * x);
        double poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
        return 1.0 - Math.Exp(-0.5 * x * x) / Math.Sqrt(2.0 * Math.PI) * poly;
    }

    // generate a correlated pair of standard normal variables
    public static (double z1, double z2) CorrelatedPair(double rho)
    {
        if (rho < -1.0 || rho > 1.0)
            throw new ArgumentOutOfRangeException(nameof(rho));

        var (e1, e2) = BoxMuller();
        return (e1, rho * e1 + Math.Sqrt(1.0 - rho * rho) * e2);
    }

    // van der corput sequence generates a sequence of numbers between 0 and 1
    public static double VanDerCorput(int n, int baseVal)
    {
        if (n < 0)
            throw new ArgumentOutOfRangeException(nameof(n));
        if (baseVal < 2)
            throw new ArgumentOutOfRangeException(nameof(baseVal));

        double value = 0.0;
        double factor = 1.0 / baseVal;

        while (n > 0)
        {
            value += (n % baseVal) * factor;
            n /= baseVal;
            factor /= baseVal;
        }

        return value;
    }

    // transform two van der corput uniforms into a pair of normal variables
    public static (double z1, double z2) BoxMullerVanDerCorput(
        int n,
        int base1,
        int base2)
    {
        double u1 = VanDerCorput(n, base1);
        double u2 = VanDerCorput(n, base2);
        u1 = Math.Max(u1, 1e-12);

        double r = Math.Sqrt(-2.0 * Math.Log(u1));
        double angle = 2.0 * Math.PI * u2;
        return (r * Math.Cos(angle), r * Math.Sin(angle));
    }
}
