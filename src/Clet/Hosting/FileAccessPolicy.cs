namespace Clet;

/// <summary>
/// Enforces file-access confinement for <c>clet md</c> to mitigate agent-context exfiltration.
/// Files must pass extension allowlist + working-directory confinement unless explicitly opted in
/// via <c>--allow-file</c>. Binary content and oversized files/aggregates are also rejected.
/// Pass <c>allowAllExtensions: true</c> to skip the extension check (used by <c>clet edit</c>).
/// </summary>
internal sealed class FileAccessPolicy
{
    /// <summary>Default allowed extensions for markdown-like content.</summary>
    private static readonly HashSet<string> DefaultAllowedExtensions = new (StringComparer.OrdinalIgnoreCase)
    {
        ".md", ".markdown", ".txt",
    };

    /// <summary>Per-file size cap: 16 MiB.</summary>
    internal const long MaxFileSizeBytes = 16L * 1024 * 1024;

    /// <summary>Aggregate size cap across all glob-expanded files: 32 MiB.</summary>
    internal const long MaxAggregateSizeBytes = 32L * 1024 * 1024;

    /// <summary>Bytes to inspect for binary (NUL) detection.</summary>
    internal const int BinaryProbeBytes = 8 * 1024;

    /// <summary>Maximum number of files from a single glob expansion.</summary>
    internal const int MaxGlobFiles = 128;

    private readonly string _workingDirectory;
    private readonly HashSet<string> _allowedFiles;
    private readonly List<string> _allowedDirs;
    private readonly bool _allowBinary;
    private readonly bool _allowAllExtensions;

    public FileAccessPolicy (string workingDirectory, IReadOnlyList<string>? allowedFiles, bool allowBinary, bool allowAllExtensions = false)
    {
        _workingDirectory = Path.GetFullPath (workingDirectory);
        _allowBinary = allowBinary;
        _allowAllExtensions = allowAllExtensions;
        _allowedFiles = new HashSet<string> (StringComparer.OrdinalIgnoreCase);
        _allowedDirs = [];

        if (allowedFiles is not null)
        {
            foreach (string f in allowedFiles)
            {
                string full = Path.GetFullPath (f);

                if (Directory.Exists (full))
                {
                    _allowedDirs.Add (full);
                }
                else
                {
                    _allowedFiles.Add (full);
                }
            }
        }
    }

    /// <summary>
    /// Checks whether a resolved file path is permitted under the policy.
    /// Returns null if permitted; an error message string if refused.
    /// </summary>
    public string? CheckFile (string filePath)
    {
        string fullPath = Path.GetFullPath (filePath);

        // Explicitly allowed files/directories bypass extension and cwd checks
        if (_allowedFiles.Contains (fullPath) || _allowedDirs.Any (dir => IsUnderDirectory (fullPath, dir)))
        {
            return CheckSizeAndBinary (fullPath);
        }

        // Extension allowlist (skipped when allowAllExtensions is true, e.g. for clet edit)
        if (!_allowAllExtensions)
        {
            string ext = Path.GetExtension (fullPath);

            if (!DefaultAllowedExtensions.Contains (ext))
            {
                return $"Refused: '{filePath}' has extension '{ext}' which is not in the allowlist " +
                       $"({string.Join (", ", DefaultAllowedExtensions)}). Use --allow-file to override.";
            }
        }

        // Working directory confinement
        if (!IsUnderDirectory (fullPath, _workingDirectory))
        {
            return $"Refused: '{filePath}' is outside the working directory '{_workingDirectory}'. " +
                   "Use --allow-file to override.";
        }

        return CheckSizeAndBinary (fullPath);
    }

    /// <summary>
    /// Validates a glob expansion: max file count and aggregate size.
    /// Returns null if OK; an error message if refused.
    /// </summary>
    public string? CheckGlobAggregate (IReadOnlyList<string> files)
    {
        if (files.Count > MaxGlobFiles)
        {
            return $"Refused: glob matched {files.Count} files, exceeding the maximum of {MaxGlobFiles}.";
        }

        long total = 0;

        foreach (string file in files)
        {
            FileInfo fi = new (file);

            if (fi.Exists)
            {
                total += fi.Length;
            }

            if (total > MaxAggregateSizeBytes)
            {
                return $"Refused: aggregate file size exceeds {MaxAggregateSizeBytes / (1024 * 1024)} MiB limit.";
            }
        }

        return null;
    }

    private string? CheckSizeAndBinary (string fullPath)
    {
        if (!File.Exists (fullPath))
        {
            return null; // Let downstream handle file-not-found
        }

        FileInfo fi = new (fullPath);

        if (fi.Length > MaxFileSizeBytes)
        {
            return $"Refused: '{fi.Name}' is {fi.Length / (1024 * 1024)} MiB, exceeding the {MaxFileSizeBytes / (1024 * 1024)} MiB per-file limit.";
        }

        if (!_allowBinary)
        {
            try
            {
                byte[] probe = new byte[Math.Min (BinaryProbeBytes, (int)fi.Length)];
                using FileStream fs = new (fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                int read = fs.Read (probe, 0, probe.Length);

                for (int i = 0; i < read; i++)
                {
                    if (probe[i] == 0)
                    {
                        return $"Refused: '{fi.Name}' appears to be a binary file (NUL byte at offset {i}). Use --allow-binary to override.";
                    }
                }
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or System.Security.SecurityException
                                       or NotSupportedException
                                       or PathTooLongException)
            {
                return $"Refused: could not probe '{fi.Name}' for binary content: {ex.Message}";
            }
        }

        return null;
    }

    private static bool IsUnderDirectory (string filePath, string directory)
    {
        // Normalize both paths fully before comparison
        string normalizedFile = Path.GetFullPath (filePath);
        string normalizedDir = Path.GetFullPath (directory);

        // Ensure directory ends with separator to prevent prefix matching attacks
        // (e.g. "/safe" matching "/safe-other-dir/file.txt")
        if (!normalizedDir.EndsWith (Path.DirectorySeparatorChar))
        {
            normalizedDir += Path.DirectorySeparatorChar;
        }

        return normalizedFile.StartsWith (normalizedDir, StringComparison.OrdinalIgnoreCase)
               || normalizedFile.Equals (normalizedDir.TrimEnd (Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
    }
}
