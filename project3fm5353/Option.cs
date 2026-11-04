// stores the market parameters and payoff calculation for an option

using System;
public class Option
{
    // stores the type of option
    public string Type { get; }
    public double S { get; }
    public double K { get; }
    public double T { get; }
    public double R { get; }
    public double Sigma { get; }

    // stores the payoff function for the option
    public IOptionPayoff Payoff { get; }

    public Option(string type, double s, double k, double t, double r, double sigma, IOptionPayoff payoff)
    {
        // make sure an option type is provided
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("option type is required");

        // stock price cannot be negative, empty, or infinity
        if (s <= 0 || double.IsNaN(s) || double.IsInfinity(s))
            throw new ArgumentOutOfRangeException(nameof(s));

        // strike price cannot be negative, empty, or infinity
        if (k < 0 || double.IsNaN(k) || double.IsInfinity(k))
            throw new ArgumentOutOfRangeException(nameof(k));

        // time to expiration must be greater than 0
        if (t <= 0 || double.IsNaN(t) || double.IsInfinity(t))
            throw new ArgumentOutOfRangeException(nameof(t));
        if (double.IsNaN(r) || double.IsInfinity(r))
            throw new ArgumentOutOfRangeException(nameof(r));
        if (sigma <= 0 || double.IsNaN(sigma) || double.IsInfinity(sigma))
            throw new ArgumentOutOfRangeException(nameof(sigma));

        // make sure a payoff calculation was provided
        Payoff = payoff ?? throw new ArgumentNullException(nameof(payoff));

        Type = type.Trim().ToLowerInvariant();
        S = s;
        K = k;
        T = t;
        R = r;
        Sigma = sigma;
    }
}
