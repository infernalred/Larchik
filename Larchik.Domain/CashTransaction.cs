namespace Larchik.Domain;

public class CashTransaction
{
    public Guid Id { get; set; }
    public decimal Income { get; set; }
    public decimal Expense { get; set; }
    public Operation Operation { get; set; }
    public string OperationId { get; set; }
    public Cash Cash { get; set; }
    public Guid CashId { get; set; }
    public DateTime CreatedAt { get; set; }
}