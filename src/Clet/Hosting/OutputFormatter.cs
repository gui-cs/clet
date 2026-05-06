using System.Text.Json.Nodes;

namespace Clet;

internal static class OutputFormatter
{
    public static bool Write (BoxedCletResult result, bool jsonOutput, TextWriter stdout, TextWriter stderr, string? outputPath = null)
    {
        TextWriter target;

        if (outputPath is not null)
        {
            try
            {
                target = new StreamWriter (outputPath, append: false, encoding: System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                stderr.WriteLine ($"error: cannot write to '{outputPath}': {ex.Message}");

                return false;
            }
        }
        else
        {
            target = stdout;
        }

        try
        {
            WriteCore (result, jsonOutput, target, stderr);
        }
        finally
        {
            if (outputPath is not null)
            {
                target.Dispose ();
            }
        }

        return true;
    }

    private static void WriteCore (BoxedCletResult result, bool jsonOutput, TextWriter target, TextWriter stderr)
    {
        if (jsonOutput)
        {
            target.WriteLine (ToSchemaV1 (result).ToJson ());

            return;
        }

        switch (result.Status)
        {
            case CletRunStatus.Ok:
                if (result.Value is not null)
                {
                    switch (result.Value)
                    {
                        case JsonArray arr:
                            foreach (JsonNode? item in arr)
                            {
                                target.WriteLine (item?.ToString ());
                            }

                            break;
                        case JsonNode node:
                            target.WriteLine (node.ToJsonString ());

                            break;
                        default:
                            target.WriteLine (result.Value);

                            break;
                    }
                }

                break;
            case CletRunStatus.Cancelled:
                break;
            case CletRunStatus.NoResult:
                break;
            case CletRunStatus.Error:
                stderr.WriteLine ($"error: {result.ErrorCode}: {result.ErrorMessage}");

                break;
        }
    }

    public static SchemaV1 ToSchemaV1 (BoxedCletResult result)
    {
        return result.Status switch
        {
            CletRunStatus.Ok => SchemaV1.Ok (result.Value),
            CletRunStatus.Cancelled => SchemaV1.Cancelled (),
            CletRunStatus.NoResult => SchemaV1.NoResult (),
            CletRunStatus.Error => SchemaV1.Error (
                result.ErrorCode ?? "unknown",
                result.ErrorMessage ?? string.Empty),
            _ => SchemaV1.Error ("unknown", $"unexpected status {result.Status}"),
        };
    }
}
