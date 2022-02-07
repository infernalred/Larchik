using Larchik.Application.Dtos;
using Larchik.Domain;

namespace Larchik.Application.Helpers;

public class MappingProfiles : AutoMapper.Profile
{
    public MappingProfiles()
    {
        CreateMap<BrokerDto, Broker>().ReverseMap();
        CreateMap<BrokerAccountDto, Account>().ReverseMap();
        CreateMap<AssetDto, Asset>().ReverseMap();
        CreateMap<CashDto, Cash>().ReverseMap();
    }
}