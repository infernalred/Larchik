using Larchik.Application.Dtos;
using Larchik.Domain;

namespace Larchik.Application.Helpers;

public class MappingProfiles : AutoMapper.Profile
{
    public MappingProfiles()
    {
        CreateMap<AccountDto, Account>().ReverseMap();
        CreateMap<AssetDto, Asset>().ReverseMap();
        CreateMap<Stock, StockDto>()
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.CurrencyId))
            //.ForMember(d => d.Type, o => o.MapFrom(s => s.TypeId))
            .ForMember(d => d.Sector, o => o.MapFrom(s => s.SectorId));
        CreateMap<DealDto, Deal>()
            .ForMember(d => d.Amount, o => o.MapFrom((_, _, _, context) => context.Options.Items["Amount"]))
            .ForMember(d => d.OperationId, o => o.MapFrom(s => s.Operation))
            .ForMember(d => d.StockId, o => o.MapFrom(s => s.Stock))
            .ForMember(d => d.CurrencyId, o => o.MapFrom(s => s.Currency))
            .ForMember(d => d.Stock, o => o.Ignore())
            .ForMember(d => d.Operation, o => o.Ignore())
            .ForMember(d => d.User, o => o.Ignore())
            .ForMember(d => d.Currency, o => o.Ignore());
        CreateMap<Deal, DealDto>()
            .ForMember(d => d.Operation, o => o.MapFrom(s => s.OperationId))
            .ForMember(d => d.Stock, o => o.MapFrom(s => s.StockId))
            .ForMember(d => d.Currency, o => o.MapFrom(s => s.CurrencyId));
    }
}