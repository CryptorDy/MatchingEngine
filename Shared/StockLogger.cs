using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Text;

namespace Stock.Shared
{
    public class StockLogger : ILogger
    {
        private readonly string _name;
        private readonly StockLoggerConfiguration _config;

        public StockLogger(string name, StockLoggerConfiguration config)
        {
            _name = name;
            _config = config;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel == _config.LogLevel;
            //return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                Log log = new Log();

                log.Application = _config.Application;
                log.EventId = eventId.Id;
                log.Level = logLevel;
                log.Source = _name;
                log.Date = DateTime.Now;

                if (exception != null)
                {
                    log.Message = exception.Message;
                    log.Exception = exception.ToString();
                }
                else
                {
                    log.Message = formatter(state, exception);
                }

                HttpClient client = new HttpClient();

                string json = JsonConvert.SerializeObject(log);
                var result = client.PostAsync(new Uri(new Uri(_config.LogService), "api/log"), new StringContent(json, Encoding.UTF8, "application/json"));
                string res = result.Result.Content.ReadAsStringAsync().Result;
            }
            catch (Exception)
            {
            }
        }
    }

    public class StockLoggerProvider : ILoggerProvider
    {
        private readonly StockLoggerConfiguration _config;
        private readonly ConcurrentDictionary<string, StockLogger> _loggers = new ConcurrentDictionary<string, StockLogger>();

        public StockLoggerProvider(StockLoggerConfiguration config)
        {
            _config = config;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _loggers.GetOrAdd(categoryName, name => new StockLogger(name, _config));
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    public static class StockLoggerExtensions
    {
        public static ILoggerFactory AddStockLogger(this ILoggerFactory loggerFactory, StockLoggerConfiguration config)
        {
            loggerFactory.AddProvider(new StockLoggerProvider(config));
            return loggerFactory;
        }
    }

    public class StockLoggerConfiguration
    {
        public LogLevel LogLevel { get; set; } = LogLevel.Error | LogLevel.Critical;

        //public int EventId { get; set; } = 0;
        public string Application { get; set; }

        public string LogService { get; set; }
    }

    public class Log
    {
        public Guid Id { get; set; }
        public LogLevel Level { get; set; }
        public int EventId { get; set; }
        public string Exception { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string Application { get; set; }
        public DateTime Date { get; set; }
    }
}