using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace RaspberryPiFileServer.Services
{
    
    public class JsonLoggerService : ILogger
    {
        private readonly string _logDirectory;
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private readonly string _name;
        private readonly IHttpContextAccessor _httpContextAccessor;

       

        public JsonLoggerService(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, string name = "Default")
        {
            _logDirectory = configuration["LogDirectory"] ?? Path.Combine(Directory.GetCurrentDirectory(), "Logs");
            _httpContextAccessor = httpContextAccessor;
            // Console.WriteLine($"{name} logger created");
            _name = name;
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }
        }

        private string GetLogFileName()
        {
            return Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyy_MM_dd}.json");
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;

            var logEntry = new
            {
                Timestamp = DateTime.Now,
                Level = logLevel.ToString(),
                EventId = eventId.Id,
                Category = _name,
                Message = formatter(state, exception),
                Exception = exception?.ToString(),
                State = state?.ToString(),
                IpAddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "Unknown"
            };

            LogToFileAsync(logEntry).GetAwaiter().GetResult();
        }



        private async Task LogToFileAsync(object logEntry)
        {
            var logFileName = GetLogFileName();
            await _semaphore.WaitAsync();

            try
            {
                List<object> existingLogs = new List<object>();
                if (File.Exists(logFileName))
                {
                    var existingContent = await File.ReadAllTextAsync(logFileName);
                    if (!string.IsNullOrEmpty(existingContent))
                    {
                        existingLogs = JsonSerializer.Deserialize<List<object>>(existingContent) ?? new List<object>();
                    }
                }

                existingLogs.Add(logEntry);
                await File.WriteAllTextAsync(logFileName, JsonSerializer.Serialize(existingLogs, new JsonSerializerOptions
                {
                    WriteIndented = true
                }));
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}