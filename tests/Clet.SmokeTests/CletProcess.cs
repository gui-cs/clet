using System.Diagnostics;

namespace Clet.SmokeTests;

internal static class CletProcess
{
    private static readonly string CletAssemblyPath = LocateCletAssembly ();

    public static async Task<(int exitCode, string stdout, string stderr)> RunAsync (
        IEnumerable<string> args,
        TimeSpan? processTimeout = null,
        string? stdin = null)
    {
        ProcessStartInfo psi = new ()
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = stdin is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.Environment["DisableRealDriverIO"] = "1";

        psi.ArgumentList.Add ("exec");
        psi.ArgumentList.Add (CletAssemblyPath);

        foreach (string a in args)
        {
            psi.ArgumentList.Add (a);
        }

        using Process process = new ();
        process.StartInfo = psi;
        process.Start ();

        if (stdin is not null)
        {
            await process.StandardInput.WriteAsync (stdin);
            process.StandardInput.Close ();
        }

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

        foreach (string name in new[] { "clet.dll", "Clet.dll" })
        {
            string candidate = Path.Combine (testDir, name);

            if (File.Exists (candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException (
            $"clet.dll not found next to smoke test assembly at '{testDir}'. " +
            "Ensure the project reference is wired and the build copied clet.dll.");
    }
}
