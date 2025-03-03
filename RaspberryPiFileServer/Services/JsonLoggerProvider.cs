using Microsoft.Extensions.Logging;

namespace RaspberryPiFileServer.Services
{
    public class JsonLoggerProvider : ILoggerProvider
    {
        private readonly IConfiguration _configuration;
         private readonly IHttpContextAccessor _httpContextAccessor; 
        private readonly Dictionary<string, JsonLoggerService> _loggers = new();
        private readonly object _lock = new();

        public JsonLoggerProvider(IConfiguration configuration,IHttpContextAccessor httpContextAccessor)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
        }

        public ILogger CreateLogger(string categoryName)
        {
            lock (_lock)
            {
                if (!_loggers.TryGetValue(categoryName, out var logger))
                {
                    logger = new JsonLoggerService(_configuration, _httpContextAccessor, categoryName);
                    _loggers[categoryName] = logger;
                }
                return logger;
            }
        }

        public void Dispose()
        {
            _loggers.Clear();
        }
    }

    public static class JsonLoggerExtensions
    {
        public static ILoggingBuilder AddJsonLogger(this ILoggingBuilder builder)
        {
            builder.Services.AddSingleton<ILoggerProvider, JsonLoggerProvider>();
            return builder;
        }
    }
}