using AutoMapper;
using MatchingEngine.Models;
using TLabs.ExchangeSdk.Trading;

namespace MatchingEngine.Helpers
{
    /// <summary>
    /// Automatically picked up by services.AddAutoMapper()
    /// </summary>
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Order, MatchingOrder>(MemberList.None);
            CreateMap<MatchingOrder, Bid>();
            CreateMap<MatchingOrder, Ask>();
            CreateMap<MatchingOrder, OrderEvent>(MemberList.None);
        }
    }
}
