namespace Larchik.Domain;

public class Exchange
{
    public long Id { get; set; }
    public string Code { get; set; } = null!;
    public int Nominal { get; set; }
    public double Rate { get; set; }
    public DateOnly Date { get; set; }
}