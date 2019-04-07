using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Stock.Trading.Data;
using Stock.Trading.Service;

namespace Stock.Trading
{
    public class Program
    {
        public static void Main(string[] args)
        {
            IWebHost host = BuildWebHost(args);

            using (var scope = host.Services.CreateScope())
            {
                var dbInitializer = scope.ServiceProvider.GetService<IDbInitializer>();
                var env = scope.ServiceProvider.GetService<IHostingEnvironment>();

                if (env.EnvironmentName == "Testing")
                {
                    dbInitializer.Init();
                    dbInitializer.Seed();
                }
                else
                {
                    dbInitializer.Seed();
                }
            }

            var matchingPool = host.Services.GetService<MatchingPoolAccessor>().MatchingPool;
            var liquidityExpireWatcher = host.Services.GetService<LiquidityExpireWatcherAccessor>().LiquidityExpireWatcher;
            liquidityExpireWatcher.SetMatchingPool(matchingPool);
            var innerBotExpireWatcher = host.Services.GetService<LiquidityExpireWatcherAccessor>().InnerBotExpireWatcher;
            innerBotExpireWatcher.SetMatchingPool(matchingPool);

            host.Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls("http://localhost:6101/")
                .Build();
    }
}
