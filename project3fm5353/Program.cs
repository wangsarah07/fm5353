// to run the program, use 'dotnet run' in the terminal and input the parameters
// this is the interface between the user and engine
// it handles all user input from the terminal
// creates an option object and starts the engine
// it runs the simulation and prints the result

using System;
using System.Globalization;
class Program
{
    static void Main()
    {
        try
        {
            RunProgram();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"error: {ex.Message}");
        }
    }

    private static void RunProgram()
    {
        // display the different types of options to the user 
        Console.WriteLine("option types:");
        Console.WriteLine("european");
        Console.WriteLine("asian");
        Console.WriteLine("digital");
        Console.WriteLine("barrier");
        Console.WriteLine("lookback");
        Console.WriteLine("range");

        // ask the user which type of option they want to price
        string type = ReadString("option type: ").ToLowerInvariant();

        if (type != "european" && type != "asian" && type != "digital" && type != "barrier" && type != "lookback" && type != "range")
            throw new ArgumentException("invalid option type");

        // range options do not use call / put direction
        bool isCall = true;
        if (type != "range")
        {
            // ask the user if they want to price a call or put
            string side = ReadString("call or put: ").ToLowerInvariant();
            if (side != "call" && side != "put")
                throw new ArgumentException("enter call or put");
            isCall = side == "call";
        }

        // get the current stock price, which must be greater than 0
        Console.Write("stock price: ");
        double S = ReadPositiveDouble();

        // get the strike price, which we do not need for range options
        double K = 0.0;
        if (type != "range")
        {
            Console.Write("strike price: ");
            K = ReadNonNegativeDouble();
        }

        Console.Write("time to expiration (years): ");
        double T = ReadPositiveDouble();

        Console.Write("risk free rate (decimal): ");
        double r = ReadDouble();

        Console.Write("volatility (decimal): ");
        double sigma = ReadPositiveDouble();

        // create the payoff object for the selected option
        IOptionPayoff payoff;

        switch (type)
        {
            case "european":
                payoff = new EuropeanPayoff(isCall, K);
                break;

            case "asian":
                payoff = new AsianPayoff(isCall, K);
                break;

            // digital options pay a fixed amount if the option finishes in the money
            case "digital":
                Console.Write("fixed payout amount: ");
                double payout = ReadNonNegativeDouble();
                payoff = new DigitalPayoff(isCall, K, payout);
                break;

            case "barrier":
                // read the barrier type
                Console.WriteLine("barrier types: upandout, downandout, upandin, downandin");
                string barrierInput = ReadString("barrier type: ")
                    .Replace("-", "")
                    .Replace(" ", "")
                    .ToLowerInvariant();

                // throw an error if the user does not select one of the options
                BarrierType barrierType = barrierInput switch
                {
                    "upandout" => BarrierType.UpAndOut,
                    "downandout" => BarrierType.DownAndOut,
                    "upandin" => BarrierType.UpAndIn,
                    "downandin" => BarrierType.DownAndIn,
                    _ => throw new ArgumentException("invalid barrier type")
                };

                Console.Write("barrier level: ");
                double barrier = ReadPositiveDouble();

                // create the barrier payoff based on the selected type and level 
                payoff = new BarrierPayoff(isCall, K, barrier, barrierType);
                break;

            case "lookback":
                payoff = new LookbackPayoff(isCall, K);
                break;

            case "range":
                payoff = new RangePayoff();
                break;

            default:
                throw new ArgumentException("invalid option type");
        }

        Console.Write("use van der corput? (yes/no): ");
        bool useVDC = ReadYesNo();

        Console.Write("use multithreading? (yes/no): ");
        bool useMultithreading = ReadYesNo();

        int paths;
        int base1 = 0;
        int base2 = 0;

        if (useVDC)
        {
            paths = 10000;
            Console.WriteLine("van der corput uses 10000 paths");

            Console.Write("enter base 1 (prime number): ");
            base1 = ReadInt();
            Console.Write("enter base 2 (different prime number): ");
            base2 = ReadInt();

            // the bases should be different prime numbers
            if (!IsPrime(base1) || !IsPrime(base2) || base1 == base2)
                throw new ArgumentException("bases must be distinct prime numbers");
        }
        else
        {
            Console.Write("number of paths: ");
            paths = ReadInt();
            if (paths < 2)
                throw new ArgumentException("number of paths must be at least two");
        }

        Console.Write("number of steps per path: ");
        int steps = ReadInt();
        if (steps < 1)
            throw new ArgumentException("number of steps must be at least one");

        Console.Write("use antithetic? (yes/no): ");
        bool useAntithetic = ReadYesNo();

        Console.Write("use control variate? (yes/no): ");
        bool useControlVariate = ReadYesNo();

        // create the option object
        Option option = new Option(type, S, K, T, r, sigma, payoff);

        // create the engine that performs the simulation and pass in user's inputs 
        Engine engine = new Engine(option, paths, steps, useAntithetic, useControlVariate, useMultithreading, useVDC, base1, base2);

        Console.WriteLine();
        Console.WriteLine("running simulation...");
        var result = engine.RunSimulation();

        Console.WriteLine();
        Console.WriteLine("simulation results");
        Console.WriteLine($"option type: {type}");
        Console.WriteLine($"price: {result.price:F6}");

        if (result.stdErr.HasValue)
            Console.WriteLine($"standard error: {result.stdErr.Value:F6}");
        else
            Console.WriteLine("standard error: n/a (not computed for van der corput)");

        Console.WriteLine($"delta: {result.delta:F6}");
        Console.WriteLine($"gamma: {result.gamma:F6}");
        Console.WriteLine($"vega: {result.vega:F6}");
        Console.WriteLine($"theta: {result.theta:F6}");
        Console.WriteLine($"rho: {result.rho:F6}");
    }

    // this reads a string from the user and removes extra spaces from the input
    private static string ReadString(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine()?.Trim()
            ?? throw new ArgumentException("input is required");
    }

    // reads a number from the user, which allows for decimals and negative values 
    private static double ReadDouble()
    {
        string input = Console.ReadLine()
            ?? throw new ArgumentException("input is required");

        // convert the input into a double
        if (!double.TryParse(input, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double value) ||
            double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException("enter a valid number");

        return value;
    }

    // reads a positive number from the user
    private static double ReadPositiveDouble()
    {
        double value = ReadDouble();

        // reject zero and negative values 
        if (value <= 0)
            throw new ArgumentException("value must be greater than zero");
        return value;
    }

    // reads a number that is zero or greater
    private static double ReadNonNegativeDouble()
    {
        double value = ReadDouble();
        if (value < 0)
            throw new ArgumentException("value cannot be negative");
        return value;
    }

    // reads an integer from the user
    private static int ReadInt()
    {
        string input = Console.ReadLine()
            ?? throw new ArgumentException("input is required");
        if (!int.TryParse(input, out int value))
            throw new ArgumentException("enter a valid integer");
        return value;
    }

    // reads a yes / no response from the user to convert into a boolean
    private static bool ReadYesNo()
    {
        string input = Console.ReadLine()?.Trim().ToLowerInvariant()
            ?? throw new ArgumentException("input is required");
        if (input == "yes" || input == "y") return true;
        if (input == "no" || input == "n") return false;
        throw new ArgumentException("enter yes or no");
    }

    // checks if a number is prime
    private static bool IsPrime(int n)
    {
        if (n < 2) return false;
        // checks for prime numbers up to the square root of n
        for (int i = 2; i * i <= n; i++)
            if (n % i == 0) return false;
        return true;
    }
}
