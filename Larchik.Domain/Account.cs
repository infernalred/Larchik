namespace Larchik.Domain;

public class Account
{
    public Guid Id { get; set; }
    public AppUser User { get; set; }
    public Broker Broker { get; set; }
    public ICollection<Asset> Assets { get; set; }
    public ICollection<Money> Money { get; set; }
}