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

            var singletonsAccessor = host.Services.GetService<SingletonsAccessor>();
            var matchingPool = singletonsAccessor.MatchingPool;
            singletonsAccessor.LiquidityExpireWatcher.SetMatchingPool(matchingPool);
            singletonsAccessor.InnerBotExpireWatcher.SetMatchingPool(matchingPool);
            matchingPool.SetDealEndingSender(singletonsAccessor.DealEndingSender);

            Console.WriteLine($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
            host.Run();
        }

        public static IWebHost BuildWebHost(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .UseUrls("http://localhost:6101/")
                .Build();
    }
}
