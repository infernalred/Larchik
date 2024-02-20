using Larchik.Application.Dtos;
using Larchik.Persistence.Models;

namespace Larchik.Application.Helpers;

public class MappingProfiles : AutoMapper.Profile
{
    public MappingProfiles()
    {
        CreateMap<AccountDto, Account>().ReverseMap();
        CreateMap<AssetDto, Asset>().ReverseMap();
        CreateMap<Stock, StockDto>()
            .ForMember(d => d.CurrencyId, o => o.MapFrom(s => s.CurrencyId))
            .ForMember(d => d.SectorId, o => o.MapFrom(s => s.SectorId));
        // CreateMap<DealDto, Operation>()
        //     .ForMember(d => d.Amount, o => o.MapFrom((_, _, _, context) => context.Items["Amount"]))
        //     .ForMember(d => d.TypeId, o => o.MapFrom(s => s.Type))
        //     .ForMember(d => d.StockId, o => o.MapFrom(s => s.Stock))
        //     .ForMember(d => d.CurrencyId, o => o.MapFrom(s => s.Currency))
        //     .ForMember(d => d.Stock, o => o.Ignore())
        //     .ForMember(d => d.User, o => o.Ignore())
        //     .ForMember(d => d.Currency, o => o.Ignore())
        //     .ForMember(d => d.Type, o => o.Ignore());
        // CreateMap<Operation, DealDto>()
        //     .ForMember(d => d.Type, o => o.MapFrom(s => s.TypeId))
        //     .ForMember(d => d.Stock, o => o.MapFrom(s => s.StockId))
        //     .ForMember(d => d.Currency, o => o.MapFrom(s => s.CurrencyId));
    }
}