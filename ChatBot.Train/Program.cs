using Azure.Monitor.OpenTelemetry.Exporter;
using ChatBot.Data;
using ChatBot.Data.Messaging;
using ChatBot.Data.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// ChatBot.Data's tokenizer/model/training/generation/chat services - the same set
// ChatBot.Api registers - so training here behaves identically to training in-process.
builder.Services.AddChatBotDataServices();

builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("AzureStorage"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("AzureServiceBus"));
builder.Services.AddSingleton<IServiceBusPublisher, ServiceBusPublisher>();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();
