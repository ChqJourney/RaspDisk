using RaspberryPiFileServer.Middlewares;
using RaspberryPiFileServer.Services;
using Microsoft.AspNetCore.HttpOverrides;
using RaspberryPiFileServer.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

// 添加SignalR服务
builder.Services.AddSignalR(options => 
{
    // 针对树莓派环境优化SignalR配置
    options.EnableDetailedErrors = true;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15); // 降低保活间隔
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30); // 客户端超时时间
    options.MaximumReceiveMessageSize = 102400; // 100KB
});

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// 配置Kestrel服务器
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 104857600; // 100MB in bytes
});

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100*1024*1024; // 100MB in bytes
    options.Limits.MaxRequestBufferSize=100*1024*1024;
    options.Limits.KeepAliveTimeout=TimeSpan.FromSeconds(500);
    options.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(500);
});

// 配置日志系统
builder.Logging.AddJsonLogger();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 配置转发头，以支持反向代理
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// app.UseHttpsRedirection();
app.UseCors("AllowFrontend"); // Enable CORS

// 启用静态文件服务
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSimpleAuth();

// 添加全局异常处理中间件
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "未处理的异常: {Message}", ex.Message);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "服务器内部错误", message = ex.Message});
    }
});

// 使用CORS中间件
app.UseCors("AllowFrontend");

// 使用路由中间件
app.UseRouting();

// 使用授权中间件
app.UseAuthorization();

// 映射控制器
app.MapControllers();

// 映射SignalR Hub
app.MapHub<TransferHub>("/transferHub");

app.Run();
