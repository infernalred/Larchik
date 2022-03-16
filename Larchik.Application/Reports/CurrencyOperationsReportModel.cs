namespace Larchik.Application.Reports;

public class CurrencyOperationsReportModel
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<CurrencyDealsReport> Operations { get; set; }
}

public class CurrencyDealsReport
{
    public string Currency { get; set; }
    public string Operation { get; set; }
    public decimal Amount { get; set; }
}