using FtpManager.Api.Services;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);
const long maxRequestBytes = 2L * 1024 * 1024 * 1024;
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = maxRequestBytes);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = maxRequestBytes);

// 1. Controller Desteğini Ekleme
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// 2. Swagger/OpenAPI Ayarları (Geliştirme aşamasında API'yi test etmek için)
builder.Services.AddEndpointsApiExplorer();

// 3. Bağımlılık Enjeksiyonu (Dependency Injection)
builder.Services.AddHttpContextAccessor();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});
builder.Services.AddSingleton<ILogService, LogService>();
builder.Services.AddSingleton<AccessService>();
builder.Services.AddSingleton<OpenSshSftpProvisioner>();
builder.Services.AddSingleton<NgrokTunnelService>();
builder.Services.AddSingleton<TrashService>();
builder.Services.AddSingleton<LocalFtpServer>();
builder.Services.AddHostedService<LocalFtpServer>(provider => provider.GetRequiredService<LocalFtpServer>());
builder.Services.AddScoped<FtpService>();

// 4. CORS (Cross-Origin Resource Sharing) Politikası
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactCorsPolicy", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// 6. HTTPS Yönlendirmesi
if (!app.Environment.IsDevelopment() && !app.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}

// 7. CORS Politikasının Devreye Alınması
app.UseCors("ReactCorsPolicy");
app.UseRateLimiter();

// 8. Yetkilendirme (Authorization) Middleware'i
app.UseAuthorization();

// 9. Rotaların Eşleştirilmesi
app.MapControllers();
app.MapHealthChecks("/health");

// 10. Uygulamayı Başlatma
app.Run();
