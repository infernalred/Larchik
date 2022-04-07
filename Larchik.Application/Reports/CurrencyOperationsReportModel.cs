namespace Larchik.Application.Reports;

public class CurrencyOperationsReportModel
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<CurrencyDealsReport> Operations { get; set; }
}

public class CurrencyDealsReport
{
    public string Account { get; set; }
    public string Currency { get; set; }
    public string Operation { get; set; }
    public decimal Amount { get; set; }
}