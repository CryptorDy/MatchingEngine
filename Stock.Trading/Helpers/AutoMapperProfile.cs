using AutoMapper;
using MatchingEngine.Models;

namespace MatchingEngine.Helpers
{
    /// <summary>
    /// Automatically picked up by services.AddAutoMapper()
    /// </summary>
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<TLabs.ExchangeSdk.Trading.Order, MatchingOrder>(MemberList.None);
            CreateMap<MatchingOrder, Bid>();
            CreateMap<MatchingOrder, Ask>();
            CreateMap<MatchingOrder, OrderEvent>(MemberList.None);
        }
    }
}
