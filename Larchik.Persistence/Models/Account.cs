namespace Larchik.Persistence.Models;

public class Account
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = null!;

    public AppUser? User { get; set; }
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Operation> Operations { get; set; } = new List<Operation>();
}