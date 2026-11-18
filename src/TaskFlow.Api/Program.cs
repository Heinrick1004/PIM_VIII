using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using TaskFlow.Api.Authentication;
using TaskFlow.Api.Endpoints;
using TaskFlow.Application;
using TaskFlow.Application.Contracts;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();

if (args.Contains("--bootstrap-admin", StringComparer.Ordinal))
{
    await AdminBootstrapCommand.RunAsync(builder);
    return;
}

var jwtSettings = JwtSettings.FromConfiguration(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Name = "Authorization",
        Description = "Informe o token JWT obtido em POST /api/auth/login."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddSingleton<SystemInfo>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddScoped<AdminBootstrapper>();
builder.Services.AddScoped<DemoAdminSeeder>();
builder.Services.AddTaskFlowInfrastructure(builder.Configuration);
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(jwtSettings.SigningKey),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "sub",
            RoleClaimType = "role"
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var subject = context.Principal?.FindFirstValue("sub");
                if (!Guid.TryParse(subject, out var userId))
                {
                    context.Fail("Token subject is invalid.");
                    return;
                }

                var users = context.HttpContext.RequestServices.GetRequiredService<IUsuarioRepository>();
                var user = await users.GetByIdAsync(userId, context.HttpContext.RequestAborted);
                if (user is null)
                {
                    context.Fail("The token user no longer exists.");
                    return;
                }

                var identity = new ClaimsIdentity(
                [
                    new Claim("sub", user.Id.ToString()),
                    new Claim("role", user.Perfil.ToString())
                ],
                    JwtBearerDefaults.AuthenticationScheme,
                    "sub",
                    "role");
                context.Principal = new ClaimsPrincipal(identity);
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrator", policy =>
        policy.RequireAuthenticatedUser().RequireRole("Administrador"));
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("DemoAdmin:Enabled"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var context = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
    var databaseName = context.Database.GetDbConnection().Database;
    if (!string.Equals(databaseName, "TaskFlowDemo", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            "A cria\u00e7\u00e3o da conta de demonstra\u00e7\u00e3o est\u00e1 restrita ao banco TaskFlowDemo.");
    }

    if (!await context.Database.CanConnectAsync())
    {
        throw new InvalidOperationException("N\u00e3o foi poss\u00edvel conectar ao banco TaskFlowDemo.");
    }

    var demoAdmin = builder.Configuration.GetSection("DemoAdmin");
    var name = demoAdmin["Name"];
    var email = demoAdmin["Email"];
    var password = demoAdmin["Password"];
    if (string.IsNullOrWhiteSpace(name)
        || string.IsNullOrWhiteSpace(email)
        || string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException(
            "Configure DemoAdmin:Name, DemoAdmin:Email e DemoAdmin:Password para habilitar o seed local.");
    }

    var created = await scope.ServiceProvider.GetRequiredService<DemoAdminSeeder>()
        .EnsureAdministratorAsync(name, email, password);
    app.Logger.LogInformation(
        created
            ? "A conta administrativa de demonstra\u00e7\u00e3o foi criada para {Email}."
            : "A conta administrativa de demonstra\u00e7\u00e3o para {Email} j\u00e1 existe; nenhuma altera\u00e7\u00e3o foi feita.",
        email);
}

app.UseExceptionHandler();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Liveness do processo: não atesta conexão com banco ou serviços externos.
app.MapGet("/health", (TimeProvider clock) => Results.Ok(new
{
    status = "Healthy",
    timestampUtc = clock.GetUtcNow().UtcDateTime
}))
.WithName("GetHealth")
.AllowAnonymous();

app.MapGet("/ready", async (TaskFlowDbContext database, CancellationToken cancellationToken) =>
{
    try
    {
        return await database.Database.CanConnectAsync(cancellationToken)
            ? Results.Ok(new { status = "Ready" })
            : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }
})
.WithName("GetReadiness")
.AllowAnonymous();

app.MapGet("/api/info", (SystemInfo info) => Results.Ok(info))
    .WithName("GetApiInfo")
    .AllowAnonymous();

app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapTaskEndpoints();

app.Run();

public partial class Program;
