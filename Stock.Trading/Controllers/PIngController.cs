using Microsoft.AspNetCore.Mvc;

namespace Stock.Trading.Controllers
{
    [Route("api/[controller]")]
    public class PingController : Controller
    {
        /// <summary>
        /// Returns OK. That`s all.
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public IActionResult Index()
        {
            return Ok();
        }
    }
}