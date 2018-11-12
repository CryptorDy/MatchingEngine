using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.PlatformAbstractions;
using Stock.Shared;
using Stock.Trading.Data;
using Stock.Trading.HttpClients;
using Stock.Trading.Service;
using Swashbuckle.AspNetCore.Swagger;
using System.IO;
using System.Linq;

namespace Stock.Trading
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
            services.AddDbContext<TradingDbContext>(options =>
                options.UseNpgsql(Configuration["ConnectionStrings:DefaultConnection"]));

            services.AddTransient<MarketDataService>();
            services.AddTransient<BrokerageService>();
            services.AddTransient<TradingService>();

            services.AddScoped<IDbInitializer, DbInitializer>();

            services.AddSingleton<IHostedService, MatchingPool>();
            services.AddSingleton<IHostedService, MarketDataSender>();
            services.AddSingleton<IHostedService, LiquidityExpireWatcher>();
            services.AddSingleton<IHostedService, InnerBotExpireWatcher>();
            services.AddSingleton<MarketDataHolder>();

            services.AddSingleton<IConfigurationRoot>(Configuration);
            services.AddSingleton<GatewayHttpClient>();
            services.AddSingleton<OrdersMatcher>();
            services.AddTransient<MarketDataSenderAccessor>();
            services.AddTransient<MatchingPoolAccessor>();
            services.AddTransient<LiquidityExpireWatcherAccessor>();
            services.AddTransient<ILiquidityImportService, LiquidityImportService>();

            services.Configure<AppSettings>(Configuration);
            services.Configure<AppSettings>(settings => settings.ConnectionString = Configuration.GetSection("ConnectionStrings:DefaultConnection").Value);
            
            // lowercase routing
            services.AddRouting(options => options.LowercaseUrls = true);
            services.AddMvc();

            var basePath = PlatformServices.Default.Application.ApplicationBasePath;

            //Set the comments path for the swagger json and ui.
            var xmlPath = Path.Combine(basePath, "Stock.Trading.xml");

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Info { Title = "Transactions", Version = "v1" });
                c.IncludeXmlComments(xmlPath);
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddStockLogger(new StockLoggerConfiguration() { Application = "Trading", LogService = Configuration["LogServiceUrl"], LogLevel = LogLevel.Debug });

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }


            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Trading V1");
            });

            app.UseMvc();
        }
    }
}
