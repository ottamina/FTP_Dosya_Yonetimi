using FtpManager.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Controller Desteğini Ekleme
builder.Services.AddControllers();

// 2. Swagger/OpenAPI Ayarları (Geliştirme aşamasında API'yi test etmek için)
builder.Services.AddEndpointsApiExplorer();

// 3. Bağımlılık Enjeksiyonu (Dependency Injection)
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ILogService, LogService>();
builder.Services.AddSingleton<AccessService>();
builder.Services.AddSingleton<LocalFtpServer>();
builder.Services.AddHostedService<LocalFtpServer>(provider => provider.GetRequiredService<LocalFtpServer>());
builder.Services.AddScoped<FtpService>();

// 4. CORS (Cross-Origin Resource Sharing) Politikası
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173") // Vite ile çalışan React uygulamasının adresi
              .AllowAnyHeader()                     // Tüm HTTP header'larına (Content-Type vb.) izin ver
              .AllowAnyMethod();                    // GET, POST, PUT, DELETE gibi tüm metotlara izin ver
    });
});

var app = builder.Build();

// 6. HTTPS Yönlendirmesi
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 7. CORS Politikasının Devreye Alınması
app.UseCors("ReactCorsPolicy");

// 8. Yetkilendirme (Authorization) Middleware'i
app.UseAuthorization();

// 9. Rotaların Eşleştirilmesi
app.MapControllers();

// 10. Uygulamayı Başlatma
app.Run();
