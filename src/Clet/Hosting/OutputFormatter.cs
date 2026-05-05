namespace Clet;

internal static class OutputFormatter
{
    public static void Write (BoxedCletResult result, bool jsonOutput, TextWriter stdout, TextWriter stderr)
    {
        if (jsonOutput)
        {
            stdout.WriteLine (ToSchemaV1 (result).ToJson ());

            return;
        }

        switch (result.Status)
        {
            case CletRunStatus.Ok:
                if (result.Value is not null)
                {
                    stdout.WriteLine (result.Value);
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
