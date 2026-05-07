using System.Reflection;

namespace Clet;

/// <summary>
/// Shared assembly-version helpers used by <c>CommandLineRoot --version</c> and <c>HelpClet</c>.
/// </summary>
internal static class VersionInfo
{
    /// <summary>
    /// Returns the clet informational version (trimmed at the '+' build-metadata separator),
    /// falling back to the assembly version.
    /// </summary>
    public static string GetCletVersion ()
    {
        return GetAssemblyVersion (typeof (Program).Assembly, "0.0.0");
    }

    /// <summary>
    /// Returns the Terminal.Gui informational version (trimmed at the '+' build-metadata separator),
    /// falling back to the assembly version.
    /// </summary>
    public static string GetTerminalGuiVersion ()
    {
        return GetAssemblyVersion (typeof (Terminal.Gui.App.Application).Assembly, "unknown");
    }

    /// <summary>
    /// Extracts the informational version from an assembly, trimming build metadata after '+'.
    /// </summary>
    internal static string GetAssemblyVersion (Assembly assembly, string fallback)
    {
        string? informational = assembly
            .GetCustomAttributes (typeof (AssemblyInformationalVersionAttribute), false)
            .OfType<AssemblyInformationalVersionAttribute> ()
            .FirstOrDefault ()
            ?.InformationalVersion;

        if (!string.IsNullOrWhiteSpace (informational))
        {
            int plus = informational.IndexOf ('+');

            return plus >= 0 ? informational [..plus] : informational;
        }

        return assembly.GetName ().Version?.ToString (3) ?? fallback;
    }
}
