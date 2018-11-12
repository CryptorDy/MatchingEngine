using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace Stock.Trading.Controllers
{
    [Route("api/[controller]")]
    public class HealthcheckController : Controller
    {
        /// <summary>
        /// Returns assembly name to check if service is alive
        /// </summary>
        /// <returns>Assembly name</returns>
        [HttpGet]
        public IActionResult Get()
        {
            return Json(Assembly.GetEntryAssembly().GetName().Name);
        }
    }
}