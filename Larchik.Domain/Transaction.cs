namespace Larchik.Domain;

public class Transaction
{
    public Guid Id { get; set; }
    public Operation Operation { get; set; }
    public string OperationId { get; set; }
    public int Quantity { get; set; }
    public Asset Asset { get; set; }
    public Guid AssetId { get; set; }
    public DateTime CreatedAt { get; set; }
}