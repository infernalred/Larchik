using System.Text.RegularExpressions;
using Xunit;

namespace Larchik.Application.Tests.Repository;

public sealed class ScriptConsistencyTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    [Fact]
    public void ResetAllAppTables_DoesNotReference_RemovedLegacyTables()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "reset_all_app_tables.sql"));

        Assert.DoesNotContain("cash_balances", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lots", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResetImportedMarketData_DoesNotReference_RemovedPersistedLots()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "reset_imported_market_data.sql"));

        Assert.DoesNotContain("lots", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ImportReferenceData_DoesNotWrite_RemovedInstrumentPriceColumn()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "import_reference_data.sql"));

        Assert.DoesNotMatch(new Regex(@"update\s+instruments[\s\S]*?\bprice\s*=", RegexOptions.IgnoreCase), script);
        Assert.DoesNotMatch(new Regex(@"insert\s+into\s+instruments\s*\((?:[^()]|\r|\n)*\bprice\b(?:[^()]|\r|\n)*\)\s*(select|values)", RegexOptions.IgnoreCase), script);
    }

    private static string ResolveRepoRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "Larchik.sln")))
            {
                return current.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
