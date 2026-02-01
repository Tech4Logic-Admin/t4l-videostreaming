using Serilog;
using T4L.VideoSearch.Api;
using T4L.VideoSearch.Api.Auth;
using T4L.VideoSearch.Api.Infrastructure;
using T4L.VideoSearch.Api.Infrastructure.Caching;
using T4L.VideoSearch.Api.Infrastructure.Middleware;
using T4L.VideoSearch.Api.Infrastructure.Persistence;
using T4L.VideoSearch.Api.Infrastructure.Security;
using T4L.VideoSearch.Api.Infrastructure.Telemetry;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithCorrelationId()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add Global Exception Handler
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// Add Application Insights (production monitoring)
builder.Services.AddCustomApplicationInsights(builder.Configuration);

// Add services
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplicationServices(builder.Configuration);

// Add Authentication & Authorization
builder.Services.AddAuth(builder.Configuration);

// Add Rate Limiting
builder.Services.AddRateLimiting(builder.Configuration);

// Add HttpClient for external API calls (Whisper, OpenAI, etc.)
builder.Services.AddHttpClient();

// Add Caching
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
builder.Services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();
builder.Services.AddResponseCachingConfiguration();

// Add Audit Service
builder.Services.AddScoped<IAuditService, AuditService>();

// Add Controllers
builder.Services.AddControllers();

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Tech4Logic Video Search API",
        Version = "v1",
        Description = "RBAC + Multilingual Timeline Search API"
    });

    // Add dev auth headers for Swagger UI
    var useDevAuth = builder.Configuration.GetValue<bool>("FeatureFlags:UseDevAuth");
    if (useDevAuth)
    {
        c.AddSecurityDefinition("DevAuth", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Name = "X-Dev-User",
            Description = "Dev user ID (e.g., dev-admin-001, dev-uploader-001, dev-reviewer-001, dev-viewer-001)"
        });
        c.AddSecurityDefinition("DevRole", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Name = "X-Dev-Role",
            Description = "Dev role (Admin, Uploader, Reviewer, Viewer)"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                        Id = "DevAuth"
                    }
                },
                Array.Empty<string>()
            }
        });
    }
    else
    {
        // OAuth2 / Bearer token for production
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token"
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
    }
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
    {
        policy.WithOrigins(
                builder.Configuration.GetValue<string>("Cors:AllowedOrigins") ?? "http://localhost:3000")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Host=localhost;Port=5432;Database=t4l_videosearch;Username=postgres;Password=postgres", name: "database")
    .AddCheck<BlobStorageHealthCheck>("blob-storage");

var app = builder.Build();

// Configure HTTP request pipeline
app.UseExceptionHandler(); // Global exception handler - must be first
app.UseCorrelationId();
app.UseSerilogRequestLogging();

// Security headers (early in pipeline)
app.UseSecurityHeaders();

// Rate limiting
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tech4Logic Video Search API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowWeb");

// Response caching
app.UseResponseCaching();
app.UseOutputCache();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers()
    .RequireRateLimiting(RateLimitingConfiguration.GlobalPolicy);

// Health endpoints (excluded from rate limiting)
app.MapHealthChecks("/healthz");
app.MapHealthChecks("/readyz", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Apply migrations in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

Log.Information("Tech4Logic Video Search API starting...");
await app.RunAsync();

// For integration testing
public partial class Program { }
