using InventoryApi.Data;
using InventoryApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Prometheus;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Send/receive enums as names ("Manager", "Shipped") instead of numbers (2, 1).
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();

// Swagger: describe the bearer token so the "Authorize" button appears.
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the token returned by /api/auth/login"
    });
    // v11 API: the scheme reference is resolved against the current document.
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("ConnectionStrings:Default is missing.")));

// Authentication = "who are you?" (validate the JWT and read its claims)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            RoleClaimType = ClaimTypes.Role
        };
    });

// Authorization = "what are you allowed to do?" ([Authorize] checks)
builder.Services.AddAuthorization();

// Basic brute-force protection on login/register: 10 attempts per IP per minute.
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// Background worker that keeps the low-stock gauge up to date.
builder.Services.AddHostedService<LowStockMetricsWorker>();

var app = builder.Build();

// Fresh database on every startup while learning (deletes old schema/data).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureDeleted();
    db.Database.EnsureCreated();
    DbSeeder.Seed(db);
}

// /metrics needs HTTP Basic credentials so it is not public. Prometheus
// sends these via basic_auth in monitoring/prometheus/prometheus.yml.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/metrics"))
    {
        var expectedUser = builder.Configuration["Metrics:User"];
        var expectedPass = builder.Configuration["Metrics:Password"];
        var header = context.Request.Headers.Authorization.ToString();

        string? supplied = null;
        if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                supplied = Encoding.UTF8.GetString(Convert.FromBase64String(header[6..]));
            }
            catch (FormatException)
            {
                supplied = null;   // malformed credentials are simply unauthorized
            }
        }

        if (supplied != $"{expectedUser}:{expectedPass}")
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Basic realm=\"metrics\"";
            return;
        }
    }
    await next();
});

// Swagger is opt-in so it is not automatically exposed in Production.
if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Records http_requests_received_total + http_request_duration_seconds automatically.
app.UseHttpMetrics();

// Order matters: authenticate first, then check permissions, throttle, then route.
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

// Prometheus scrapes this endpoint with the Basic credentials checked above.
app.MapMetrics();

app.Run();
