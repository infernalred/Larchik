namespace Larchik.Application.Dtos;

public class AccountDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public decimal Amount { get; set; }
    public ICollection<AssetDto> Assets { get; set; }
}