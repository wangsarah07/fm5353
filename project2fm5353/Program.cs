// to run the program, use 'dotnet run' in the terminal and input the parameters
// this is the interface between the user and engine
// it handles all user input from the terminal
// creates an option object and starts the engine
// it runs the simulation and prints the result
// this gives the user the option to use antithetic, van der corput, and multithreading

using System;

class Program
{
    static void Main()
    {
        // ask the user if they want to price a call or a put
        Console.Write("option type (call/put): ");
        string type = Console.ReadLine()!.Trim().ToLower();

        Console.Write("stock price: ");
        double S = double.Parse(Console.ReadLine()!);

        Console.Write("strike price: ");
        double K = double.Parse(Console.ReadLine()!);

        Console.Write("time to expiration (years): ");
        double T = double.Parse(Console.ReadLine()!);

        Console.Write("risk free rate (decimal): ");
        double r = double.Parse(Console.ReadLine()!);

        Console.Write("volatility (decimal): ");
        double sigma = double.Parse(Console.ReadLine()!);

        Console.Write("use van der corput? (yes/no): ");
        bool useVDC = Console.ReadLine()!.Trim().ToLower() == "yes";

        Console.Write("use multithreading? (yes/no): ");
        bool useMultithreading = Console.ReadLine()!.Trim().ToLower() == "yes";

        int paths = 0;
        int steps = 0;
        bool useAntithetic = false;
        bool useControlVariate = false;
        int base1 = 0, base2 = 0;

        if (useVDC)
        {
            // van der corput uses a fixed internal path count
            // user only supplies bases, not the number of simulations
            Console.Write("enter base 1 (for van der corput): ");
            base1 = int.Parse(Console.ReadLine()!);

            Console.Write("enter base 2 (for van der corput): ");
            base2 = int.Parse(Console.ReadLine()!);
        }
        else
        {
            Console.Write("number of paths: ");
            paths = int.Parse(Console.ReadLine()!);

            Console.Write("number of steps (per path): ");
            steps = int.Parse(Console.ReadLine()!);

            Console.Write("use antithetic? (yes/no): ");
            useAntithetic = Console.ReadLine()!.Trim().ToLower() == "yes";

            Console.Write("use delta control variate? (yes/no): ");
            useControlVariate = Console.ReadLine()!.Trim().ToLower() == "yes";
        }

        // creates an object of the option class with the user inputs
        Option option = new Option(type, S, K, T, r, sigma);

        // passes all the information into the engine, which will perform the simulation
        Engine engine = new Engine(
            option,
            paths,
            steps,
            useAntithetic,
            useControlVariate,
            useMultithreading,
            useVDC,
            base1,
            base2
        );

        var result = engine.RunSimulation();

        Console.WriteLine();
        Console.WriteLine($"price: {result.price:F6}");

        // we only return a standard error when using van der corput
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
}
