using Larchik.Application.Operations.ImportBroker;
using Microsoft.Extensions.Logging.Abstractions;

namespace Larchik.Application.Tests.Tbank;

internal static class TbankReportFixtureHelper
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, BrokerReportParseResult> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TbankReportParser Parser = new(NullLogger<TbankReportParser>.Instance);

    public static string FixturesRoot { get; } = ResolveFixturesRoot();
    public static string ReferenceDataRoot { get; } = ResolveReferenceDataRoot();

    public static IEnumerable<object[]> FixtureFiles()
    {
        return Directory.EnumerateFiles(FixturesRoot, "*.xlsx")
            .Select(Path.GetFileName)
            .OrderBy(x => x, StringComparer.Ordinal)
            .Select(fileName => new object[] { fileName! });
    }

    public static BrokerReportParseResult Parse(string fileName)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(fileName, out var cached))
            {
                return cached;
            }

            var path = Path.Combine(FixturesRoot, fileName);
            using var stream = File.OpenRead(path);
            var result = Parser.ParseAsync(stream, fileName, CancellationToken.None).GetAwaiter().GetResult();
            Cache[fileName] = result;
            return result;
        }
    }

    private static string ResolveFixturesRoot()
    {
        return ResolveFromRepoRoot("tests/broker_files/tbank");
    }

    private static string ResolveReferenceDataRoot()
    {
        return ResolveFromRepoRoot("Tests/Larchik.Application.Tests/Fixtures/Tbank");
    }

    private static string ResolveFromRepoRoot(string relativePath)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, relativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, relativePath));
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"T-Bank test directory was not found: {root}");
        }

        return root;
    }
}
