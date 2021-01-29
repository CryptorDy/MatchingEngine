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
using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Swagger;
using System.IO;

namespace MatchingEngine
{
    public class Startup
    {
        public Startup(Microsoft.AspNetCore.Hosting.IHostingEnvironment env)
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
            services.AddSingleton<GatewayHttpClient>();
            services.AddSingleton<IConfigurationRoot>(Configuration);
            services.Configure<AppSettings>(Configuration);
            services.Configure<AppSettings>(settings => settings.ConnectionString = Configuration.GetSection("ConnectionStrings:DefaultConnection").Value);

            services.AddDbContext<TradingDbContext>(options =>
                options.UseNpgsql(Configuration["ConnectionStrings:DefaultConnection"]),
                ServiceLifetime.Transient);
            services.AddScoped<IDbInitializer, DbInitializer>();
            services.AddSingleton<ICurrenciesService, CurrenciesService>();
            services.AddTransient<SingletonsAccessor>();

            services.AddSingleton<IHostedService, MatchingPoolsHandler>();
            services.AddSingleton<OrdersMatcher>();
            services.AddTransient<TradingService>();
            services.AddHostedService<DealEndingSender>();
            services.AddSingleton<MarketDataHolder>();
            services.AddHostedService<MarketDataSender>();
            services.AddTransient<MarketDataService>();
            services.AddTransient<IDealEndingService, DealEndingService>();

            services.AddTransient<ILiquidityImportService, LiquidityImportService>();
            services.AddSingleton<IHostedService, LiquidityExpireWatcher>();
            services.AddSingleton<LiquidityExpireBlocksHandler>();
            services.AddHostedService<LiquidityExpireBlocksBgService>();
            services.AddSingleton<ILiquidityDeletedOrdersKeeper, LiquidityDeletedOrdersKeeper>();
            services.AddHostedService<InnerBotExpireWatcher>();

            services.AddAutoMapper(typeof(Startup));

            // lowercase routing
            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddMvc()
                .AddJsonOptions(options =>
                {
                    options.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                })
                .AddFluentValidation(fvc =>
                    fvc.RegisterValidatorsFromAssemblyContaining<Startup>());

            var basePath = PlatformServices.Default.Application.ApplicationBasePath;

            //Set the comments path for the swagger json and ui.
            var xmlPath = Path.Combine(basePath, "MatchingEngine.xml");

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "MatchingEngine", Version = "v1" });
                c.IncludeXmlComments(xmlPath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env,
            ILoggerFactory loggerFactory, IMapper mapper)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }

            mapper.ConfigurationProvider.AssertConfigurationIsValid();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MatchingEngine V1");
            });

            app.UseMvc();
        }
    }
}
