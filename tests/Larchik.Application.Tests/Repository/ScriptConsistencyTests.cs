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

    [Theory]
    [InlineData("RU000A10F850")]
    [InlineData("RU000A10FA72")]
    [InlineData("RU000A10BV55")]
    [InlineData("RU000A10FMQ8")]
    [InlineData("RU000A10FJS0")]
    [InlineData("RU000A10FNA0")]
    [InlineData("RU000A10FNB8")]
    [InlineData("RU000A10FMY2")]
    [InlineData("RU000A10FGH9")]
    [InlineData("RU000A10CS75")]
    [InlineData("RU000A10FTR1")]
    public void ImportReferenceData_IncludesLatestMoexBonds(string isin)
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "import_reference_data.sql"));

        Assert.Contains(isin, script, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SU26246RMFS7", "RU000A108EE1")]
    [InlineData("SU26250RMFS9", "RU000A10BVH7")]
    [InlineData("SU26253RMFS3", "RU000A10D517")]
    [InlineData("SU26254RMFS1", "RU000A10D533")]
    [InlineData("SU29015RMFS3", "RU000A1025A7")]
    [InlineData("TGLD@", "RU000A101X50")]
    public void ImportReferenceData_IncludesBrokerStatementAliases(string alias, string ticker)
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "import_reference_data.sql"));

        Assert.Contains($"'{alias}', '{alias}', '{ticker}'", script, StringComparison.Ordinal);
    }

    [Fact]
    public void TTechnologiesPriceSourceFix_DoesNotMatchByAmbiguousTicker()
    {
        var script = File.ReadAllText(Path.Combine(RepoRoot, "scripts", "fix_t_technologies_price_source.sql"));

        Assert.Contains("RU000A107UL4", script, StringComparison.Ordinal);
        Assert.Contains("BBG000BSJK37", script, StringComparison.Ordinal);
        Assert.Contains("ticker = 'T-US'", script, StringComparison.Ordinal);
        Assert.Contains("'T@US'", script, StringComparison.Ordinal);
        Assert.Contains("price_source = 'MOEX'", script, StringComparison.Ordinal);
        Assert.Contains("price_source = 'TBANK'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE upper(coalesce(ticker, '')) = 'T'", script, StringComparison.OrdinalIgnoreCase);
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
