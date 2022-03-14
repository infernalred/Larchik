using Larchik.Application.Dtos;
using Larchik.Domain;

namespace Larchik.Application.Helpers;

public class MappingProfiles : AutoMapper.Profile
{
    public MappingProfiles()
    {
        CreateMap<BrokerDto, Broker>().ReverseMap();
        CreateMap<AccountDto, Account>().ReverseMap();
        CreateMap<AssetDto, Asset>().ReverseMap();
        CreateMap<Stock, StockDto>()
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.Currency.Code))
            .ForMember(d => d.Type, o => o.MapFrom(s => s.Type.Code))
            .ForMember(d => d.Sector, o => o.MapFrom(s => s.Sector.Code));
        CreateMap<DealDto, Deal>()
            .ForMember(d => d.AccountId, o => o.MapFrom((_, _, _, context) => context.Options.Items["AccountId"]))
            .ForMember(d => d.Amount, o => o.MapFrom((_, _, _, context) => context.Options.Items["Amount"]))
            .ForMember(d => d.CreatedAt, o => o.MapFrom(s => DateTime.UtcNow))
            .ForMember(d => d.OperationId, o => o.MapFrom(s => s.Operation))
            .ForMember(d => d.StockId, o => o.MapFrom(s => s.Stock))
            .ForMember(d => d.Stock, o => o.Ignore())
            .ForMember(d => d.Operation, o => o.Ignore());
    }
}