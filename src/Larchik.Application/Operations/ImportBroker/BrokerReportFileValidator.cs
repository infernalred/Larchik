namespace Larchik.Application.Operations.ImportBroker;

public static class BrokerReportFileValidator
{
    public static string? ValidateXlsx(
        Stream fileStream,
        string fileName,
        string invalidExtensionMessage,
        string invalidFormatMessage)
    {
        if (!HasSupportedExtension(fileName, ".xlsx"))
        {
            return invalidExtensionMessage;
        }

        if (!LooksLikeZipArchive(fileStream))
        {
            return invalidFormatMessage;
        }

        return null;
    }

    private static bool HasSupportedExtension(string fileName, params string[] extensions) =>
        extensions.Any(x => string.Equals(Path.GetExtension(fileName), x, StringComparison.OrdinalIgnoreCase));

    private static bool LooksLikeZipArchive(Stream fileStream)
    {
        if (!fileStream.CanSeek)
        {
            return true;
        }

        var originalPosition = fileStream.Position;
        Span<byte> signature = stackalloc byte[4];

        try
        {
            fileStream.Position = 0;
            var read = fileStream.Read(signature);
            if (read < 4)
            {
                return false;
            }

            return signature[0] == (byte)'P'
                && signature[1] == (byte)'K'
                && ((signature[2] == 3 && signature[3] == 4)
                    || (signature[2] == 5 && signature[3] == 6)
                    || (signature[2] == 7 && signature[3] == 8));
        }
        finally
        {
            fileStream.Position = originalPosition;
        }
    }
}
