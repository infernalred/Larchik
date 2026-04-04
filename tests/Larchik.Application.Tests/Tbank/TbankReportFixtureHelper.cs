using Larchik.Application.Operations.ImportBroker;
using Microsoft.Extensions.Logging.Abstractions;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Larchik.Application.Tests.Tbank;

internal static partial class TbankReportFixtureHelper
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<string, BrokerReportParseResult> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TbankReportParser Parser = new(NullLogger<TbankReportParser>.Instance);
    private static readonly Regex ReportFileRegex = MyRegex();

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

            var path = ResolveFixturePath(fileName);
            using var stream = File.OpenRead(path);
            var result = Parser.ParseAsync(stream, fileName, CancellationToken.None).GetAwaiter().GetResult();
            Cache[fileName] = result;
            return result;
        }
    }

    public static string ResolveFixturePath(string fileName)
    {
        var exactPath = Path.Combine(FixturesRoot, fileName);
        if (File.Exists(exactPath))
        {
            return exactPath;
        }

        var requestedPeriod = TryParseReportPeriod(fileName);
        if (requestedPeriod is null)
        {
            throw new FileNotFoundException($"T-Bank fixture file was not found: {exactPath}", exactPath);
        }

        var fallback = Directory.EnumerateFiles(FixturesRoot, "broker-report-*.xlsx")
            .Select(path => new
            {
                Path = path,
                FileName = Path.GetFileName(path),
                Period = TryParseReportPeriod(Path.GetFileName(path))
            })
            .Where(x => x.Period is not null)
            .Where(x => x.Period!.Value.Start == requestedPeriod.Value.Start)
            .Where(x => x.Period!.Value.End >= requestedPeriod.Value.End)
            .OrderBy(x => x.Period!.Value.End)
            .FirstOrDefault();

        return fallback?.Path
               ?? throw new FileNotFoundException($"T-Bank fixture file was not found: {exactPath}", exactPath);
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

    private static (DateOnly Start, DateOnly End)? TryParseReportPeriod(string fileName)
    {
        var match = ReportFileRegex.Match(fileName);
        if (!match.Success)
        {
            return null;
        }

        return (
            DateOnly.ParseExact(match.Groups["start"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            DateOnly.ParseExact(match.Groups["end"].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    [GeneratedRegex(@"^broker-report-(?<start>\d{4}-\d{2}-\d{2})-(?<end>\d{4}-\d{2}-\d{2})\.xlsx$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "en-US")]
    private static partial Regex MyRegex();
}
