using System.Text.Json.Serialization;
using BLComponent.Abstractions;
using BLComponent.Configuration;
using BLComponent.Managers;
using BLComponent.Scanner;
using BLComponent.StateMachine;
using BLComponent.Workers;
using DBComponent.Queues;
using DBComponent.Registries;
using DBComponent.Repositories;
using Serilog;
using WebComponent.ExceptionHandlers;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

builder.Services
    .AddOptions<ScanningOptions>()
    .Bind(builder.Configuration.GetSection(ScanningOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ScanQueueUnavailableExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IVulnerabilityRepository, VulnerabilityRepository>();
builder.Services.AddSingleton<IScanRepository, ScanRepository>();
builder.Services.AddSingleton<IScanQueue, ScanQueue>();
builder.Services.AddSingleton<IRunningScanRegistry, RunningScanRegistry>();

builder.Services.AddSingleton<IScanner, StubScanner>();
builder.Services.AddSingleton<IScanStateMachine, ScanStateMachine>();

builder.Services.AddScoped<IVulnerabilityManager, VulnerabilityManager>();
builder.Services.AddScoped<IScanManager, ScanManager>();

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

var scanningOptions = builder.Configuration
    .GetSection(ScanningOptions.SectionName)
    .Get<ScanningOptions>() ?? new ScanningOptions();

for (var i = 0; i < scanningOptions.MaxParallelScans; i++)
    builder.Services.AddSingleton<IHostedService, ScanWorker>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} -> {StatusCode} in {Elapsed:0.0000} ms";
});

app.MapControllers();

app.Logger.LogInformation(
    "Vulnerability Tracker API starting. MaxParallelScans={MaxParallelScans}",
    scanningOptions.MaxParallelScans);

await app.RunAsync();
