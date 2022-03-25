namespace Larchik.Domain;

public class Account
{
    public Guid Id { get; set; }
    public AppUser User { get; set; }
    public string UserId { get; set; }
    public string Name { get; set; }
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}