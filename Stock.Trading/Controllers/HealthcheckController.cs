using Microsoft.AspNetCore.Mvc;
using System;
using System.Reflection;

namespace MatchingEngine.Controllers
{
    [Route("api/[controller]")]
    public class HealthcheckController : Controller
    {
        /// <summary>
        /// Returns assembly name to check if service is alive
        /// </summary>
        /// <returns>Assembly info</returns>
        [HttpGet]
        public IActionResult Get()
        {
            Console.WriteLine($"HealthcheckController Get begin");
            var assemblyName = Assembly.GetExecutingAssembly().GetName();
            return Json($"{assemblyName.Name} v{assemblyName.Version}");
        }
    }
}
