using Larchik.Persistence.Models;

namespace Larchik.Domain;

public class Account
{
    public Guid Id { get; set; }
    public AppUser User { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<Deal> Deals { get; set; } = new List<Deal>();
}