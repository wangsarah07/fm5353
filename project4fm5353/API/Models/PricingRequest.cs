namespace MonteCarloApi.Models;

// stores the parameters sent to the API for one option-pricing request
public class PricingRequest
{
    // option parameters
    public string? OptionType { get; set; }
    public string? Side { get; set; }
    public double? StockPrice { get; set; }
    public double? StrikePrice { get; set; }
    public double? TimeToExpiration { get; set; }
    public double? RiskFreeRate { get; set; }
    public double? Volatility { get; set; }

    // additional parameters used by specific exotic options
    public double? Payout { get; set; }
    public string? BarrierType { get; set; }
    public double? BarrierLevel { get; set; }

    // simulation settings
    public int? NumberOfPaths { get; set; }
    public int? NumberOfSteps { get; set; }
    public bool UseAntithetic { get; set; }
    public bool UseControlVariate { get; set; }
    public bool UseMultithreading { get; set; }
    public bool UseVanDerCorput { get; set; }
    public int? Base1 { get; set; }
    public int? Base2 { get; set; }
}
