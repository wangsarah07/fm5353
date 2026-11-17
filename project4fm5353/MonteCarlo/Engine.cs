// this is the simulation engine for pricing options
// it simulates asset price paths using geometric brownian motion
// computes option prices as a discounted expected payoff
// estimates greeks using finite differences
// computes the standard error of standard monte carlo simulations
// the engine takes an option object as input and performs all the work
// it does not handle user input or output

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class Engine
{
    // store option parameters and simulation settings
    // readonly: values are fixed after the constructor runs
    private readonly Option _option;
    private readonly int _numPaths;
    private readonly int _numSteps;
    private readonly bool _useAntithetic;
    private readonly bool _useControlVariate;
    private readonly bool _useMultithreading;
    private readonly bool _useVanDerCorput;
    private readonly int _base1;
    private readonly int _base2;

    // fixed number of simulations used by van der corput
    private const int VdcNumPaths = 10000;

    // constructor receives the option ands simulation settings
    public Engine(Option option, int numPaths, int numSteps, bool useAntithetic, bool useControlVariate, bool useMultithreading, bool useVanDerCorput, int base1, int base2)
    {
        // make sure an option was provided
        _option = option ?? throw new ArgumentNullException(nameof(option));

        // make sure there are enough paths / time steps
        if (numPaths < 2)
            throw new ArgumentOutOfRangeException(nameof(numPaths));
        if (numSteps < 1)
            throw new ArgumentOutOfRangeException(nameof(numSteps));

        _numPaths = numPaths;
        _numSteps = numSteps;
        _useAntithetic = useAntithetic;
        _useControlVariate = useControlVariate;
        _useMultithreading = useMultithreading;
        _useVanDerCorput = useVanDerCorput;
        _base1 = base1;
        _base2 = base2;
    }

    // pricing method that runs the monte carlo simulation
    // returns null standard error when using van der corput
    public (double price, double? stdErr, double delta, double gamma, double vega, double theta, double rho) RunSimulation()
    {
        return _useVanDerCorput
            ? RunVdcSimulation()
            : RunStandardSimulation();
    }

    // standard monte carlo simulation
    private (double price, double? stdErr, double delta, double gamma, double vega, double theta, double rho) RunStandardSimulation()
    {
        // generate the random shocks once and reuse them to calculate the greeks
        double[][] shocks = GenerateStandardShocks();

        return RunWithShocks(shocks, true);
    }

    // van der corput simulation
    // this uses one shock per path to simulate the terminal stock price
    private (double price, double? stdErr, double delta, double gamma, double vega, double theta, double rho) RunVdcSimulation()
    {
        double[][] shocks = GenerateVdcShocks();
        return RunWithShocks(shocks, false);
    }

    // calculate the price and all greeks using the same random shocks
    // reusing shocks makes the finite difference estimates less noisy
    private (double price, double? stdErr, double delta, double gamma, double vega, double theta, double rho) RunWithShocks(
            double[][] shocks,
            bool calculateStdErr)
    {
        // finite bump sizes used for computing greeks
        double dS = _option.S * 0.01;

        double dSig = Math.Min(
            Math.Max(_option.Sigma * 0.01, 1e-5),
            _option.Sigma * 0.5);

        double dr = 0.0001;
        double dT = 1.0 / 365.0;

        // calculate the base option price
        var baseResult = PriceScenario(
            _option.S, _option.Sigma, _option.R, _option.T, shocks);

        // calculate delta and gamma by bumping the stock price
        var priceUp = PriceScenario(
            _option.S + dS, _option.Sigma, _option.R, _option.T, shocks);

        var priceDown = PriceScenario(
            _option.S - dS, _option.Sigma, _option.R, _option.T, shocks);

        double delta = (priceUp.price - priceDown.price) / (2.0 * dS);
        double gamma = (priceUp.price - 2.0 * baseResult.price + priceDown.price) / (dS * dS);

        // calculate vega using an upward and downward volatility bump
        var vegaUp = PriceScenario(
            _option.S, _option.Sigma + dSig, _option.R, _option.T, shocks);

        var vegaDown = PriceScenario(
            _option.S, _option.Sigma - dSig, _option.R, _option.T, shocks);

        double vega = (vegaUp.price - vegaDown.price) / (2.0 * dSig);

        // calculate rho by bumping the risk-free interest rate
        var rhoUp = PriceScenario(
            _option.S, _option.Sigma, _option.R + dr, _option.T, shocks);

        var rhoDown = PriceScenario(
            _option.S, _option.Sigma, _option.R - dr, _option.T, shocks);

        double rho = (rhoUp.price - rhoDown.price) / (2.0 * dr);

        // calculate theta by reducing time to maturity by one day
        double theta = 0.0;

        if (_option.T > dT)
        {
            var shorterMaturity = PriceScenario(_option.S, _option.Sigma, _option.R, _option.T - dT, shocks);

            theta = (shorterMaturity.price - baseResult.price) / dT;
        }

        // standard error is not reported for van der corput
        double? stdErr = calculateStdErr ? baseResult.stdErr : null;

        return (baseResult.price, stdErr, delta, gamma, vega, theta, rho);
    }

    // generate normal shocks for standard monte carlo
    private double[][] GenerateStandardShocks()
    {
        // create an array to store the shocks for each path
        double[][] shocks = new double[_numPaths][];

        Action<int> generatePath = i =>
        {
            shocks[i] = new double[_numSteps];

            for (int step = 0; step < _numSteps; step++)
            {
                var (z, _) = RandomGenerators.BoxMullerThreadSafe();
                shocks[i][step] = z;
            }
        };

        // generate paths using multithreading
        if (_useMultithreading)
        {
            Parallel.For(
                0,
                _numPaths,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                generatePath);
        }
        else
        {
            for (int i = 0; i < _numPaths; i++)
                generatePath(i);
        }

        return shocks;
    }

    // generate one low-discrepancy normal shock for every path
    private double[][] GenerateVdcShocks()
    {
        // create an array to store the shocks
        double[][] shocks = new double[VdcNumPaths][];

        // define work needed to generate one path
        Action<int> generatePath = i =>
        {
            // use van der corput to generate one normal shock
            var (z, _) = RandomGenerators.BoxMullerVanDerCorput(i + 1, _base1, _base2);

            shocks[i] = new double[1];
            shocks[i][0] = z;
        };

        // generate paths using multithreading
        if (_useMultithreading)
        {
            Parallel.For(0, VdcNumPaths, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, generatePath);
        }
        else
        {
            for (int i = 0; i < VdcNumPaths; i++)
                generatePath(i);
        }

        return shocks;
    }

    // price an option using a specified set of random shocks
    // this method is also used for every Greek calculation
    private (double price, double stdErr) PriceScenario(
        double S0,
        double sigma,
        double r,
        double maturity,
        double[][] shocks)
    {
        int paths = shocks.Length;
        int steps = shocks[0].Length;
        double dt = maturity / steps;
        double drift = (r - 0.5 * sigma * sigma) * dt;
        double discount = Math.Exp(-r * maturity);

        // these arrays hold one observation per simulated path
        // when antithetic is enabled, each observation is the pair average
        double[] discountedPayoffs = new double[paths];
        double[] discountedStocks = new double[paths];

        Action<int> evaluatePath = i =>
        {
            double[] zPath = shocks[i];

            double payoff = SimulatePath(
                S0, sigma, dt, drift, zPath, out double terminalPrice);

            // simulate the antithetic path using the negative shocks
            if (_useAntithetic && !_useVanDerCorput)
            {
                double[] zAnti = new double[zPath.Length];
                for (int step = 0; step < zPath.Length; step++)
                    zAnti[step] = -zPath[step];

                double antiPayoff = SimulatePath(
                    S0, sigma, dt, drift, zAnti, out double antiTerminalPrice);

                payoff = 0.5 * (payoff + antiPayoff);
                terminalPrice = 0.5 * (terminalPrice + antiTerminalPrice);
            }

            discountedPayoffs[i] = discount * payoff;
            discountedStocks[i] = discount * terminalPrice;
        };

        if (_useMultithreading)
        {
            Parallel.For(
                0,
                paths,
                new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                evaluatePath);
        }
        else
        {
            for (int i = 0; i < paths; i++)
                evaluatePath(i);
        }

        // calculate the regression coefficient for the control variate
        // the expected discounted terminal stock price is S0
        double beta = 0.0;

        if (_useControlVariate && !_useVanDerCorput)
        {
            double meanPayoff = Mean(discountedPayoffs);
            double meanStock = Mean(discountedStocks);
            double covariance = 0.0;
            double varianceStock = 0.0;

            for (int i = 0; i < paths; i++)
            {
                double dx = discountedStocks[i] - meanStock;
                double dy = discountedPayoffs[i] - meanPayoff;
                covariance += dx * dy;
                varianceStock += dx * dx;
            }

            if (varianceStock > 1e-20)
                beta = covariance / varianceStock;
        }

        // apply the same control-variate method to every scenario,
        // including the scenarios used for all finite-difference greeks
        double[] adjustedPayoffs = new double[paths];

        for (int i = 0; i < paths; i++)
        {
            adjustedPayoffs[i] = discountedPayoffs[i];

            if (_useControlVariate && !_useVanDerCorput)
            {
                adjustedPayoffs[i] -= beta * (discountedStocks[i] - S0);
            }
        }

        double price = Mean(adjustedPayoffs);
        double stdErr = StdDev(adjustedPayoffs) / Math.Sqrt(paths);

        return (price, stdErr);
    }

    // simulate a full GBM path and return its payoff
    // the complete path is passed to the payoff class so exotic options
    // can use averages, barriers, maxima, minima, or ranges
    private double SimulatePath(
        double S0,
        double sigma,
        double dt,
        double drift,
        double[] zPath,
        out double terminalPrice)
    {
        double[] prices = new double[zPath.Length + 1];
        prices[0] = S0;
        double S = S0;

        for (int step = 0; step < zPath.Length; step++)
        {
            S *= Math.Exp(drift + sigma * Math.Sqrt(dt) * zPath[step]);
            prices[step + 1] = S;
        }

        terminalPrice = S;
        return _option.Payoff.Evaluate(prices);
    }

    // calculate the mean of an array
    private double Mean(double[] arr)
    {
        double sum = 0.0;
        foreach (double value in arr)
            sum += value;

        return sum / arr.Length;
    }

    // calculate sample standard deviation
    private double StdDev(double[] arr)
    {
        if (arr.Length < 2)
            return double.NaN;

        double mean = Mean(arr);
        double sumSq = 0.0;

        foreach (double value in arr)
            sumSq += (value - mean) * (value - mean);

        return Math.Sqrt(sumSq / (arr.Length - 1));
    }
}