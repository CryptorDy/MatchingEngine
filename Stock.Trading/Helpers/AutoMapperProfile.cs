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
            CreateMap<Order, Bid>();
            CreateMap<Order, Ask>();
            CreateMap<Order, OrderEvent>(MemberList.None);
        }
    }
}
