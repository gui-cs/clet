namespace Clet;

internal static class Program
{
    public static async Task<int> Main (string[] args)
    {
        // Minimal bootstrap for v0.1 alpha — just demonstrates the registry and select clet.
        // Full CLI via System.CommandLine will come in a later milestone.

        using CancellationTokenSource cts = new ();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel (); };

        ICletRegistry registry = new CletRegistry ();
        registry.Register (new SelectClet ());

        if (args.Length == 0)
        {
            Console.WriteLine ("clet v0.1-alpha. Use --help for usage.");

            return 0;
        }

        if (args [0] == "list")
        {
            foreach (IClet clet in registry.All)
            {
                Console.WriteLine ($"{clet.PrimaryAlias} - {clet.Description}");
            }

            return 0;
        }

        Console.WriteLine ($"Unknown command: {args [0]}");

        return 2;
    }
}
