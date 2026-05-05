namespace Clet;

internal static class Program
{
    public static async Task<int> Main (string[] args)
    {
        using CancellationTokenSource cts = new ();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel ();
        };

        ICletRegistry registry = new CletRegistry ();
        BuiltInClets.RegisterAll (registry);

        CommandLineRoot root = new (registry);

        return await root.InvokeAsync (args, cts.Token, Console.Out, Console.Error);
    }
}
