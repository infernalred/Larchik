namespace Larchik.Application.Dtos;

public class AccountDto
{
    public Guid Id { get; set; }
    public BrokerDto Broker { get; set; }
    public ICollection<AssetDto> Assets { get; set; }
}