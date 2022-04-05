namespace Larchik.Application.Reports;

public class ReportParams
{
    public DateTime StartDate
    {
        get => DateTime.SpecifyKind(_startDate, DateTimeKind.Utc);
        set => _startDate = value;
    }

    private DateTime _startDate = new(DateTime.UtcNow.Year, 1, 1);

    public DateTime EndDate
    {
        get => DateTime.SpecifyKind(_endDate, DateTimeKind.Utc);
        set => _endDate = value;
    }
    private DateTime _endDate = DateTime.UtcNow;
}