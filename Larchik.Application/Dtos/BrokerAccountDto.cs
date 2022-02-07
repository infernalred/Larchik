namespace Larchik.Application.Dtos;

public class BrokerAccountDto
{
    public Guid Id { get; set; }
    public BrokerDto Broker { get; set; }
    public ICollection<AssetDto> Assets { get; set; }
    public ICollection<CashDto> Cash { get; set; }
}