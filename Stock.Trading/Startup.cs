using AutoMapper;
using FluentValidation.AspNetCore;
using Flurl.Http;
using MatchingEngine.Data;
using MatchingEngine.Helpers;
using MatchingEngine.HttpClients;
using MatchingEngine.Models;
using MatchingEngine.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;
using System.IO;
using TLabs.DotnetHelpers;
using TLabs.ExchangeSdk;
using TLabs.ExchangeSdk.Currencies;
using TLabs.ExchangeSdk.Depository;

namespace MatchingEngine
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env, ILogger<FlurlCall> flurlLogger)
        {
            var basePath = env.EnvironmentName == "Testing"
                ? Path.Combine(env.ContentRootPath, "..\\..\\..")
                : env.ContentRootPath;

            var builder = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
            CurrentEnvironment = env;
        }

        public IConfigurationRoot Configuration { get; }
        public IWebHostEnvironment CurrentEnvironment { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(Configuration);
            var settings = Configuration.Get<AppSettings>();

            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddControllers()
                .AddNewtonsoftJson(options => { options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; })
                .AddFluentValidation(fvc => fvc.RegisterValidatorsFromAssemblyContaining<Startup>());

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = CurrentEnvironment.ApplicationName, Version = "v1" });
                c.IncludeXmlComments(Path.Combine(PlatformServices.Default.Application.ApplicationBasePath,
                    $"{CurrentEnvironment.ApplicationName}.xml"));
            });
            services.AddSwaggerGenNewtonsoftSupport();

            services.AddAutoMapper(typeof(Startup));

            services.InitFlurl(settings.GatewayServiceUrl);
            services.AddSdkServices();

            services.AddSingleton<GatewayHttpClient>();
            services.AddSingleton<CurrenciesCache>();

            services.AddDbContext<TradingDbContext>(options =>
                options.UseNpgsql(Configuration["ConnectionStrings:DefaultConnection"]),
                ServiceLifetime.Transient);
            services.AddScoped<IDbInitializer, DbInitializer>();

            services.AddTransient<SingletonsAccessor>();

            services.AddHostedService<MatchingPoolsHandler>();
            services.AddSingleton<OrdersMatcher>();
            services.AddTransient<TradingService>();

            services.AddTransient<IDealEndingService, DealEndingService>();
            services.AddHostedService<DealEndingSender>();
            services.AddTransient<DealDeleteService>();

            services.AddSingleton<MarketDataHolder>();
            services.AddHostedService<MarketDataSender>();
            services.AddTransient<MarketDataService>();
            services.AddHostedService<MarketDataDealsSender>();

            services.AddTransient<ILiquidityImportService, LiquidityImportService>();
            services.AddHostedService<LiquidityExpireWatcher>();
            services.AddSingleton<LiquidityExpireBlocksHandler>();
            services.AddHostedService<LiquidityExpireBlocksBgService>();
            services.AddSingleton<ILiquidityDeletedOrdersKeeper, LiquidityDeletedOrdersKeeper>();
            services.AddHostedService<InnerBotExpireWatcher>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, ILoggerFactory loggerFactory, IMapper mapper)
        {
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{CurrentEnvironment.ApplicationName} V1");
            });

            mapper.ConfigurationProvider.AssertConfigurationIsValid();
        }
    }
}
