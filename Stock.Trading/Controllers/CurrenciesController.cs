using MatchingEngine.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace MatchingEngine.Controllers
{
    [Route("api/currencies")]
    public class CurrenciesController : Controller
    {
        private readonly ICurrenciesService _currenciesService;

        public CurrenciesController(ICurrenciesService currenciesService)
        {
            _currenciesService = currenciesService;
        }

        /// <summary>
        /// Reload currencies and currency pairs
        /// </summary>
        [HttpPost("reload")]
        public async Task Reload()
        {
            await _currenciesService.LoadData();
        }
    }
}
