using AutoMapper;
using FluentValidation.AspNetCore;
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
using TLabs.ExchangeSdk.Currencies;

namespace MatchingEngine
{
    public class Startup
    {
        public Startup(IWebHostEnvironment env)
        {
            var basePath = env.EnvironmentName == "Testing"
                ? Path.Combine(env.ContentRootPath, "..\\..\\..")
                : env.ContentRootPath;

            var builder = new ConfigurationBuilder();
            builder
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();

            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(Configuration);

            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddControllers()
                .AddNewtonsoftJson(options => { options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore; })
                .AddFluentValidation(fvc => fvc.RegisterValidatorsFromAssemblyContaining<Startup>());

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "MatchingEngine", Version = "v1" });
                c.IncludeXmlComments(Path.Combine(PlatformServices.Default.Application.ApplicationBasePath, "MatchingEngine.xml"));
            });
            services.AddSwaggerGenNewtonsoftSupport();

            services.AddAutoMapper(typeof(Startup));

            services.AddDbContext<TradingDbContext>(options =>
                options.UseNpgsql(Configuration["ConnectionStrings:DefaultConnection"]),
                ServiceLifetime.Transient);
            services.AddScoped<IDbInitializer, DbInitializer>();

            services.AddSingleton<GatewayHttpClient>();
            services.AddTransient<SingletonsAccessor>();

            services.AddSingleton<CurrenciesCache>();

            services.AddHostedService<MatchingPoolsHandler>();
            services.AddSingleton<OrdersMatcher>();
            services.AddTransient<TradingService>();

            services.AddTransient<IDealEndingService, DealEndingService>();
            services.AddHostedService<DealEndingSender>();

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
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MatchingEngine V1");
            });

            mapper.ConfigurationProvider.AssertConfigurationIsValid();
        }
    }
}
