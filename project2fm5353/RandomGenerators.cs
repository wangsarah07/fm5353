using System;

public static class RandomGenerators

// static class for generating random numbers
// this contains methods for normal and gaussian random variables

{
    // random number generator object
    private static readonly Random _rng = new Random();
    private static readonly object _rngLock = new object();

    // generate uniform random variable on [0, 1)
    private static double U() => _rng.NextDouble();

    // generate uniform random variable on (-1, 1)
    private static double U11() => _rng.NextDouble() * 2.0 - 1.0;

    // sum twelve method generates a single gaussian random variable using sum of 12 uniforms
    public static double Sum12()
    {
        double sum = 0;
        for (int i = 0; i < 12; i++) sum += U();
        return sum - 6.0;
    }

    // box muller generates a pair of gaussian random variables from two uniforms
    public static (double z1, double z2) BoxMuller()
    {
        double x1, x2;

        // keep random number generation safe when paths run on multiple threads
        lock (_rngLock)
        {
            x1 = U();
            x2 = U();
        }

        double r = Math.Sqrt(-2.0 * Math.Log(x1));
        return (r * Math.Cos(2 * Math.PI * x2),
                r * Math.Sin(2 * Math.PI * x2));
    }

    // polar rejection method generates a pair of gaussian random variables using rejection sampling
    public static (double z1, double z2) PolarRejection()
    {
        double x1, x2, w;
        do
        {
            x1 = U11();
            x2 = U11();
            w = x1 * x1 + x2 * x2;
        } while (w > 1.0);

        double c = Math.Sqrt(-2.0 * Math.Log(w) / w);
        return (c * x1, c * x2);
    }

    // gaussian cdf approximation
    public static double NormalCDF(double x)
    {
        if (x < 0) return 1.0 - NormalCDF(-x);
        const double a1 = 0.319381530, a2 = -0.356563782, a3 = 1.781477937, a4 = -1.821255978, a5 = 1.330274429, p = 0.2316419;
        double t = 1.0 / (1.0 + p * x);
        double poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
        return 1.0 - (1.0 / Math.Sqrt(2 * Math.PI)) * Math.Exp(-0.5 * x * x) * poly;
    }

    // generate a correlated pair of gaussian random variables with correlation rho
    public static (double z1, double z2) CorrelatedPair(double rho)
    {
        var (e1, e2) = BoxMuller();
        return (e1, rho * e1 + Math.Sqrt(1 - rho * rho) * e2);
    }

    // van der corput sequence for low discrepancy sequences
    public static double VanDerCorput(int n, int baseVal)
    {
        double vdc = 0.0;
        double denom = 1.0;
        while (n > 0)
        {
            denom *= baseVal;
            vdc += (n % baseVal) / denom;
            n /= baseVal;
        }
        return vdc;
    }

    // generate a pair of normal variables using van der corput and box muller
    public static (double z1, double z2) BoxMullerVanDerCorput(int n, int base1, int base2)
    {
        double u1 = VanDerCorput(n, base1);
        double u2 = VanDerCorput(n, base2);
        if (u1 <= 0.0) u1 = 1e-10;
        double r = Math.Sqrt(-2.0 * Math.Log(u1));
        return (r * Math.Cos(2 * Math.PI * u2),
                r * Math.Sin(2 * Math.PI * u2));
    }

    // each thread gets their own random number generator
    private static readonly ThreadLocal<Random> _threadRng =
        new ThreadLocal<Random>(() => 
            new Random(Guid.NewGuid().GetHashCode()));

    // generate two standard normal random variables
    public static (double z1, double z2) BoxMullerThreadSafe()
    {
        double x1 = _threadRng.Value.NextDouble();
        double x2 = _threadRng.Value.NextDouble();

        // don't take log of zero
        while (x1 <= 0.0) 
            x1 = _threadRng.Value.NextDouble();

        double r = Math.Sqrt(-2.0 * Math.Log(x1));
        double theta = 2.0 * Math.PI * x2;

        return (
            r * Math.Cos(theta),
            r * Math.Sin(theta)
        );
    }
}
