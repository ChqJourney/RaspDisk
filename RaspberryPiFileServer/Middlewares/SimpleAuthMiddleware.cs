using Microsoft.AspNetCore.Http;

namespace RaspberryPiFileServer.Middlewares
{
    public class SimpleAuthMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _password;

        public SimpleAuthMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _password = configuration["ApiPassword"] ?? "default_password";
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 只对API请求进行身份验证
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                if (!context.Request.Headers.TryGetValue("X-Api-Key", out var apiKey) || apiKey != _password)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Unauthorized");
                    return;
                }
            }

            await _next(context);
        }
    }

    public static class SimpleAuthMiddlewareExtensions
    {
        public static IApplicationBuilder UseSimpleAuth(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SimpleAuthMiddleware>();
        }
    }
}