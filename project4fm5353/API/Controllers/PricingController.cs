using Microsoft.AspNetCore.Mvc;
using MonteCarloApi.Models;

namespace MonteCarloApi.Controllers;

// api controller that provides access to the monte carlo simulator
[ApiController]
[Route("api/[controller]")]
public class PricingController : ControllerBase
{
    // run the monte carlo simulation using parameters supplied in json
    [HttpPost]
    public async Task<ActionResult<PricingResponse>> Price(
        [FromBody] PricingRequest request)
    {
        // make sure the request contains valid option parameters
        string? error = ValidateRequest(request);
        if (error != null)
            return BadRequest(new { error });

        try
        {
            // create the payoff object for the selected option
            IOptionPayoff payoff = CreatePayoff(request);

            // create the option object using the existing simulator class
            string type = request.OptionType!.Trim().ToLowerInvariant();

            Option option = new Option(
                type,
                request.StockPrice!.Value,
                type == "range" ? 0.0 : request.StrikePrice!.Value,
                request.TimeToExpiration!.Value,
                request.RiskFreeRate!.Value,
                request.Volatility!.Value,
                payoff);

            // van der corput uses a fixed number of paths in the existing engine
            int paths = request.NumberOfPaths!.Value;
            int steps = request.NumberOfSteps!.Value;

            // create the engine with the simulation settings from the request
            Engine engine = new Engine(
                option,
                paths,
                steps,
                request.UseAntithetic,
                request.UseControlVariate,
                request.UseMultithreading,
                request.UseVanDerCorput,
                request.Base1 ?? 0,
                request.Base2 ?? 0);

            // run the simulation asynchronously so the request thread is not blocked
            var result = await Task.Run(() => engine.RunSimulation());

            // create the response object containing the price and all greeks
            PricingResponse response = new PricingResponse
            {
                OptionType = type,
                Price = result.price,
                StandardError = result.stdErr,
                Delta = result.delta,
                Gamma = result.gamma,
                Vega = result.vega,
                Theta = result.theta,
                Rho = result.rho
            };

            return Ok(response);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // create the payoff calculation for the requested option type
    private static IOptionPayoff CreatePayoff(PricingRequest request)
    {
        string type = request.OptionType!.Trim().ToLowerInvariant();
        bool isCall = string.Equals(
            request.Side, "call", StringComparison.OrdinalIgnoreCase);

        double strike = type == "range" ? 0.0 : request.StrikePrice!.Value;

        switch (type)
        {
            case "european":
                return new EuropeanPayoff(isCall, strike);

            case "asian":
                return new AsianPayoff(isCall, strike);

            case "digital":
                return new DigitalPayoff(isCall, strike, request.Payout!.Value);

            case "barrier":
                BarrierType barrierType = request.BarrierType!.Trim()
                    .Replace("-", "")
                    .Replace(" ", "")
                    .ToLowerInvariant() switch
                {
                    "upandout" => BarrierType.UpAndOut,
                    "downandout" => BarrierType.DownAndOut,
                    "upandin" => BarrierType.UpAndIn,
                    "downandin" => BarrierType.DownAndIn,
                    _ => throw new ArgumentException("invalid barrier type")
                };

                return new BarrierPayoff(
                    isCall, strike, request.BarrierLevel!.Value, barrierType);

            case "lookback":
                return new LookbackPayoff(isCall, strike);

            case "range":
                return new RangePayoff();

            default:
                throw new ArgumentException("invalid option type");
        }
    }

    // check the request before creating the option and running the simulation
    private static string? ValidateRequest(PricingRequest request)
    {
        if (request == null)
            return "request body is required";

        if (string.IsNullOrWhiteSpace(request.OptionType))
            return "option type is required";

        string type = request.OptionType.Trim().ToLowerInvariant();

        if (type != "european" && type != "asian" && type != "digital" &&
            type != "barrier" && type != "lookback" && type != "range")
            return "invalid option type";

        if (type != "range" &&
            request.Side?.Trim().ToLowerInvariant() != "call" &&
            request.Side?.Trim().ToLowerInvariant() != "put")
            return "side must be call or put";

        if (!IsValidPositive(request.StockPrice))
            return "stock price must be greater than zero";

        if (type != "range" && !IsValidNonNegative(request.StrikePrice))
            return "strike price must be zero or greater";

        if (!IsValidPositive(request.TimeToExpiration))
            return "time to expiration must be greater than zero";

        if (!IsValidFinite(request.RiskFreeRate))
            return "risk free rate is required and must be a valid number";

        if (!IsValidPositive(request.Volatility))
            return "volatility must be greater than zero";

        if (type == "digital" && !IsValidNonNegative(request.Payout))
            return "digital options require a non-negative payout";

        if (type == "barrier")
        {
            if (!IsValidPositive(request.BarrierLevel))
                return "barrier level must be greater than zero";

            string barrier = request.BarrierType?.Trim()
                .Replace("-", "")
                .Replace(" ", "")
                .ToLowerInvariant() ?? "";

            if (barrier != "upandout" && barrier != "downandout" &&
                barrier != "upandin" && barrier != "downandin")
                return "invalid barrier type";
        }

        if (!request.NumberOfPaths.HasValue || request.NumberOfPaths.Value < 2)
            return "number of paths must be at least two";

        if (!request.NumberOfSteps.HasValue || request.NumberOfSteps.Value < 1)
            return "number of steps must be at least one";

        if (request.UseVanDerCorput)
        {
            if (!request.Base1.HasValue || !request.Base2.HasValue ||
                !IsPrime(request.Base1.Value) || !IsPrime(request.Base2.Value) ||
                request.Base1.Value == request.Base2.Value)
                return "van der corput requires two different prime-number bases";
        }

        return null;
    }

    private static bool IsValidFinite(double? value)
    {
        return value.HasValue &&
               !double.IsNaN(value.Value) &&
               !double.IsInfinity(value.Value);
    }

    private static bool IsValidPositive(double? value)
    {
        // check whether a number is finite and greater than zero
        return IsValidFinite(value) && value!.Value > 0.0;
    }

    private static bool IsValidNonNegative(double? value)
    {
        return IsValidFinite(value) && value!.Value >= 0.0;
    }

    // check whether a van der corput base is prime
    private static bool IsPrime(int number)
    {
        if (number < 2)
            return false;

        for (int i = 2; i * i <= number; i++)
        {
            if (number % i == 0)
                return false;
        }

        // no divisors were found, so the number is prime
        return true;
    }
}
