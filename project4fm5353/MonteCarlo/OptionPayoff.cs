using System;
using System.Linq;

// interface requires every option to have an evaluation method
// takes a stock price path and returns the payoff at expiration
public interface IOptionPayoff
{
    double Evaluate(double[] path);
}

// european options use the stock price at expiration
public class EuropeanPayoff : IOptionPayoff
{
    // store whether the option is a call or a put
    private readonly bool _isCall;
    private readonly double _strike;

    public EuropeanPayoff(bool isCall, double strike)
    {
        _isCall = isCall;
        _strike = strike;
    }

    public double Evaluate(double[] path)
    {
        // get the last stock pricea at the final time step
        double st = path[path.Length - 1];
        return _isCall
        // calculate the payoff based on if the option is a call or a put
            ? Math.Max(st - _strike, 0.0)
            : Math.Max(_strike - st, 0.0);
    }
}

// Asian options use the arithmetic average of prices after the initial time
public class AsianPayoff : IOptionPayoff
{
    private readonly bool _isCall;
    private readonly double _strike;

    public AsianPayoff(bool isCall, double strike)
    {
        _isCall = isCall;
        _strike = strike;
    }

    // calculate the payoff using the average stock price over the path
    public double Evaluate(double[] path)
    {
        double sum = 0.0;

        // add together all the stock prices after time 0
        for (int i = 1; i < path.Length; i++)
            sum += path[i];
        double average = sum / (path.Length - 1);

        return _isCall
        // calculate the payoff based on if the option is a call or a put
            ? Math.Max(average - _strike, 0.0)
            : Math.Max(_strike - average, 0.0);
    }
}

// digital options pay a fixed amount if they finish in the money
public class DigitalPayoff : IOptionPayoff
{
    private readonly bool _isCall;
    private readonly double _strike;

    // store the fixed payout amount
    private readonly double _payout;

    public DigitalPayoff(bool isCall, double strike, double payout)
    {
        // make sure the payout amount is valid
        if (payout < 0 || double.IsNaN(payout) || double.IsInfinity(payout))
            throw new ArgumentOutOfRangeException(nameof(payout));

        _isCall = isCall;
        _strike = strike;
        _payout = payout;
    }

    public double Evaluate(double[] path)
    {
        // get the final stock price
        double st = path[path.Length - 1];

        // determine whether the option is in the money
        bool inTheMoney = _isCall ? st > _strike : st < _strike;

        // if in the money, pay the fixed amount, otherwise pay nothing
        return inTheMoney ? _payout : 0.0;
    }
}

// the four supported barrier styles
public enum BarrierType
{
    UpAndOut,
    DownAndOut,
    UpAndIn,
    DownAndIn
}

// barrier options combine a european payoff with a path barrier condition
public class BarrierPayoff : IOptionPayoff
{
    private readonly bool _isCall;
    private readonly double _strike;
    private readonly double _barrier;
    private readonly BarrierType _type;

    public BarrierPayoff(bool isCall, double strike, double barrier, BarrierType type)
    {
        // make sure the barrier is a valid number
        if (barrier <= 0 || double.IsNaN(barrier) || double.IsInfinity(barrier))
            throw new ArgumentOutOfRangeException(nameof(barrier));

        _isCall = isCall;
        _strike = strike;
        _barrier = barrier;
        _type = type;
    }

    // calculate the barrier option payoff using the entire stock price path
    public double Evaluate(double[] path)
    {
        // determine if the barrier is above or below the stock price
        bool isUpBarrier =
            _type == BarrierType.UpAndOut || _type == BarrierType.UpAndIn;
        // determine whether the option becomes active or inactive after hitting the barrier 
        bool isKnockOut =
            _type == BarrierType.UpAndOut || _type == BarrierType.DownAndOut;

        // check whether the maximum or minimum price hits the barrier
        bool hit = isUpBarrier
            ? path.Max() >= _barrier
            : path.Min() <= _barrier;

        // determine if the option is active based on the barrier status
        bool active = isKnockOut ? !hit : hit;

        // if the option was knocked out, the payoff is 0
        if (!active)
            return 0.0;

        // if the option is active, calculate the payoff like a regular european
        double st = path[path.Length - 1];
        return _isCall
            ? Math.Max(st - _strike, 0.0)
            : Math.Max(_strike - st, 0.0);
    }
}

// fixed strike lookback call uses the maximum; put uses the minimum
public class LookbackPayoff : IOptionPayoff
{
    private readonly bool _isCall;
    private readonly double _strike;

    public LookbackPayoff(bool isCall, double strike)
    {
        _isCall = isCall;
        _strike = strike;
    }

    public double Evaluate(double[] path)
    {
        // find the maximum or minimum stock price over the path
        double extreme = _isCall ? path.Max() : path.Min();

        // calculate the payoff based on whether this is a call or a put
        return _isCall
            ? Math.Max(extreme - _strike, 0.0)
            : Math.Max(_strike - extreme, 0.0);
    }
}

// range options pay the maximum price minus the minimum price
public class RangePayoff : IOptionPayoff
{
    public double Evaluate(double[] path)
    {
        return path.Max() - path.Min();
    }
}
