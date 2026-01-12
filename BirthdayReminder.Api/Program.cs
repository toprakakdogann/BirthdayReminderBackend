using Microsoft.OpenApi.Models;
using BirthdayReminder.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;
using BirthdayReminder.Api.Background;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(opt =>
{
    opt.AddPolicy("client", p =>
        p.AllowAnyHeader()
         .AllowAnyMethod()
         .AllowAnyOrigin());
});

builder.Services.AddControllers();

builder.Services.AddHostedService<RefreshTokenCleanupService>();

builder.Services.AddRateLimiter(options =>
{
    // Rate limit aşılınca dönecek yanıt
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // LOGIN: IP başına 10/dk
    options.AddPolicy("auth-login", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // REGISTER: IP başına 3/5dk
    options.AddPolicy("auth-register", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 3,
            Window = TimeSpan.FromMinutes(5),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // REFRESH: IP başına 20/dk
    options.AddPolicy("auth-refresh", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // SYNC: kullanıcı başına 60/dk (authorize olduğu için userId’den partition)
    options.AddPolicy("sync", httpContext =>
    {
        var userId =
            httpContext.User.FindFirstValue("sub") ??
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });

    // LOGOUT: kullanıcı başına 30/dk (opsiyonel ama iyi)
    options.AddPolicy("auth-logout", httpContext =>
    {
        var userId =
            httpContext.User.FindFirstValue("sub") ??
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

        return RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler(options =>
{
    options.ExceptionHandlingPath = "/error";
});

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "BirthdayReminder API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT Authorization header. Example: \"Bearer {token}\""
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services
    .AddIdentityCore<AppUser>(opt =>
    {
        opt.Password.RequireDigit = true;
        opt.Password.RequireNonAlphanumeric = false;
        opt.Password.RequireUppercase = false;
        opt.Password.RequiredLength = 8;
        opt.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>();

var jwt = builder.Configuration.GetSection("Jwt");
var key = jwt["Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var enableSwagger = builder.Configuration.GetValue<bool>("Swagger:Enabled");

if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("client");
app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseRateLimiter();

app.UseAuthorization();
app.MapGet("/", () => Results.Ok(new
{
    name = "BirthdayReminder API",
    status = "ok",
    docs = "/swagger",
    health = "/health"
}));


app.MapControllers();

app.Run();
