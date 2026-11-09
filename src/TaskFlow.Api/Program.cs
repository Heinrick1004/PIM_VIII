using TaskFlow.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<SystemInfo>();
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Liveness do processo: não atesta conexão com banco ou serviços externos.
app.MapGet("/health", (TimeProvider clock) => Results.Ok(new
{
    status = "Healthy",
    timestampUtc = clock.GetUtcNow().UtcDateTime
}))
.WithName("GetHealth")
.WithOpenApi();

app.MapGet("/api/info", (SystemInfo info) => Results.Ok(info))
.WithName("GetApiInfo")
.WithOpenApi();

app.Run();

public partial class Program;
