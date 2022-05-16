namespace Larchik.Domain;

public class Account
{
    public Guid Id { get; set; }
    public AppUser User { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}