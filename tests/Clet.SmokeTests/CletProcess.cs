using System.Diagnostics;
using System.Reflection;

namespace Clet.SmokeTests;

internal static class CletProcess
{
    private static readonly string CletAssemblyPath = LocateCletAssembly ();

    public static async Task<(int exitCode, string stdout, string stderr)> RunAsync (
        IEnumerable<string> args,
        TimeSpan? processTimeout = null)
    {
        ProcessStartInfo psi = new ()
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.ArgumentList.Add ("exec");
        psi.ArgumentList.Add (CletAssemblyPath);

        foreach (string a in args)
        {
            psi.ArgumentList.Add (a);
        }

        using Process process = new () { StartInfo = psi };
        process.Start ();

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync ();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync ();

        TimeSpan timeout = processTimeout ?? TimeSpan.FromSeconds (15);
        bool exited;

        try
        {
            await process.WaitForExitAsync (new CancellationTokenSource (timeout).Token);
            exited = process.HasExited;
        }
        catch (OperationCanceledException)
        {
            exited = false;
        }

        if (!exited)
        {
            try
            {
                process.Kill (entireProcessTree: true);
            }
            catch
            {
                // Best-effort; ignored.
            }

            throw new TimeoutException ($"clet process did not exit within {timeout.TotalSeconds:F1}s.");
        }

        string stdout = await stdoutTask;
        string stderr = await stderrTask;

        return (process.ExitCode, stdout, stderr);
    }

    private static string LocateCletAssembly ()
    {
        // The smoke test project copies the Clet output via ProjectReference, so Clet.dll lives
        // alongside the test assembly when a test runs. Resolving via reflection avoids hard-coded
        // configuration/RID paths.
        string testDir = Path.GetDirectoryName (typeof (CletProcess).Assembly.Location)!;
        string candidate = Path.Combine (testDir, "Clet.dll");

        if (File.Exists (candidate))
        {
            return candidate;
        }

        throw new FileNotFoundException (
            $"Clet.dll not found next to smoke test assembly at '{testDir}'. " +
            "Ensure the project reference is wired and the build copied Clet.dll.");
    }
}
