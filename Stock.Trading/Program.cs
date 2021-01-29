using MatchingEngine.Data;
using MatchingEngine.Services;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;

namespace MatchingEngine
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
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
            host.Services.GetRequiredService<ICurrenciesService>().LoadData().Wait(); // load currencies and currency pairs
            var singletonsAccessor = host.Services.GetRequiredService<SingletonsAccessor>();
            var matchingPoolsHandler = singletonsAccessor.MatchingPoolsHandler;
            host.Services.GetRequiredService<LiquidityExpireBlocksHandler>().SetMatchingPoolsHandler(matchingPoolsHandler);
            singletonsAccessor.LiquidityExpireWatcher.SetMatchingPoolsHandler(matchingPoolsHandler);
            singletonsAccessor.InnerBotExpireWatcher.SetMatchingPoolsHandler(matchingPoolsHandler);

            host.Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls("http://0.0.0.0:6101/")
                .Build();
    }
}
