using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine.Controllers
{
    [Route("api/currencies")]
    public class CurrenciesController : Controller
    {
        private readonly CurrenciesCache _currenciesCache;

        public CurrenciesController(CurrenciesCache currenciesService)
        {
            _currenciesCache = currenciesService;
        }

        /// <summary>Reload currencies and currency pairs</summary>
        [HttpPost("reload")]
        public async Task Reload()
        {
            await _currenciesCache.LoadData();
        }
    }
}
