namespace Larchik.Domain;

public class Exchange
{
    public string Code { get; set; } = null!;
    public int Nominal { get; set; }
    public double Rate { get; set; }
    public DateOnly Date { get; set; }
}