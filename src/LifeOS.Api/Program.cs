using System.Text;
using LifeOS.Api.Middleware;
using LifeOS.Application.Interfaces;
using LifeOS.Infrastructure.AI;
using LifeOS.Infrastructure.Data;
using LifeOS.Infrastructure.Data.SeedData;
using LifeOS.Infrastructure.Identity;
using LifeOS.Infrastructure.PDF;
using LifeOS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ========================
// Configuration
// ========================
var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("JWT Secret is missing.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "LifeOS";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "LifeOS";

// ========================
// Services
// ========================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter 'Bearer {token}'"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Identity & Auth
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<ICurrentUserService, LifeOS.Api.Services.CurrentUserService>();

// AI Providers
builder.Services.AddTransient<GeminiProvider>();
builder.Services.AddTransient<OllamaProvider>();
builder.Services.AddTransient<IAiProvider>(sp =>
{
    var gemini = sp.GetRequiredService<GeminiProvider>();
    var ollama = sp.GetRequiredService<OllamaProvider>();
    var preferred = sp.GetRequiredService<IConfiguration>()["Ai:Provider"] ?? "gemini";
    var factory = new AiProviderFactory(new IAiProvider[] { gemini, ollama }, preferred);
    return factory.GetProvider();
});

// PDF & Document Generation
QuestPDF.Settings.License = LicenseType.Community;
builder.Services.AddTransient<IResumeGenerator, QuestPdfResumeGenerator>();
builder.Services.AddTransient<IResumeDataBuilder, ResumeDataBuilder>();
builder.Services.AddSingleton<IDocumentStorage>(sp =>
{
    var basePath = sp.GetRequiredService<IConfiguration>()["Documents:StoragePath"] ?? "./data/generated";
    return new LocalDocumentStorage(basePath);
});

// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ========================
// Middleware Pipeline
// ========================
app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Health check
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

// Auto-migrate on startup (self-hosted convenience)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Seed Bible data (WEB - public domain)
    var bibleSeeder = new BibleSeeder(db);
    await bibleSeeder.SeedAsync();
}

app.Run();
