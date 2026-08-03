using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderFlow.Api.Middleware;
using OrderFlow.Application;
using OrderFlow.Application.Commands.RegisterUser;
using OrderFlow.Application.Common.Interfaces;
using OrderFlow.Infrastructure;
using OrderFlow.Infrastructure.Data;
using OrderFlow.Infrastructure.Services;
using OrderFlow.Infrastructure.Data.Seed;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "OrderFlow.Api")
    .CreateLogger();

builder.Host.UseSerilog();

// ── Services ──────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();

// ── Swagger ───────────────────────────────────────────────
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OrderFlow API",
        Version = "v1",
        Description = "OrderFlow — Order management system with Clean Architecture + CQRS"
    });

    var xmlFile = $"{typeof(Program).Assembly.GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);

    options.EnableAnnotations();

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT token"
    });

    // Document filter to add {version} parameter from Swagger UI
    options.DocumentFilter<ReplaceVersionWithExactValueInPathFilter>();

});

// ── API Versioning ────────────────────────────────────────
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
    new Asp.Versioning.UrlSegmentApiVersionReader(),
    new Asp.Versioning.QueryStringApiVersionReader("api-version"),
    new Asp.Versioning.HeaderApiVersionReader("X-Api-Version"));
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

// ── OpenTelemetry ─────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "OrderFlow.Api", serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddNpgsql())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter());

// ── Rate Limiting ─────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("Api", config =>
    {
        config.PermitLimit = 100;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 10;
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        var response = new
        {
            error = "Too many requests. Please try again later.",
            retryAfter = "1 minute"
        };
        await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken);
    };
});

// ── JWT Authentication ────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException(
        "Jwt:Key is not configured. Set it via appsettings or Jwt__Key environment variable.");

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "OrderFlow.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "OrderFlow.Client";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// ── Health Checks ─────────────────────────────────────────
builder.Services.AddHealthChecks()
    .AddDbContextCheck<OrderFlowDbContext>("Database");

// ── CORS ──────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                "https://orderflow-web.vercel.app",
                "http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ── Application Layers ────────────────────────────────────
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterUserCommandHandler).Assembly));

var app = builder.Build();

// ── Middleware Pipeline ───────────────────────────────────
app.UseExceptionMiddleware();
app.UseRateLimiter();
app.UseCors("AllowFrontend");
app.UseSerilogRequestLogging();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderFlow API v1");
    options.RoutePrefix = "swagger";
});

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ── Health Check Endpoint ─────────────────────────────────
app.MapHealthChecks("/health");

// ── Prometheus Metrics Endpoint ───────────────────────────
app.UseOpenTelemetryPrometheusScrapingEndpoint();

// ── Database Bootstrap ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderFlowDbContext>();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();

    await db.Database.MigrateAsync();
    await DataSeeder.SeedAsync(db, configuration, environment);
}

// ── Application Start ─────────────────────────────────────
try
{
    Log.Information("Starting OrderFlow API");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// ── Swagger Versioning ──────────────────────────────
public class ReplaceVersionWithExactValueInPathFilter : Swashbuckle.AspNetCore.SwaggerGen.IDocumentFilter
{
    public void Apply(Microsoft.OpenApi.OpenApiDocument swaggerDoc, Swashbuckle.AspNetCore.SwaggerGen.DocumentFilterContext context)
    {
        var paths = new Microsoft.OpenApi.OpenApiPaths();
        foreach (var path in swaggerDoc.Paths)
        {
            var newKey = path.Key.Replace("{version}", "v1");
            paths[newKey] = path.Value;
        }
        swaggerDoc.Paths = paths;
    }
}

// ── Test Entry Point ──────────────────────────────────────
public partial class Program { } 
