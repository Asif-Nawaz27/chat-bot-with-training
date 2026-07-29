using Azure.Monitor.OpenTelemetry.Exporter;
using ChatBot.Data;
using ChatBot.Data.Messaging;
using ChatBot.Data.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

// AzureStorage/TrainingCallback settings live in appsettings.json (matching ChatBot.Api's
// convention) rather than local.settings.json, which is reserved for the Functions host
// bootstrap values (AzureWebJobsStorage, FUNCTIONS_WORKER_RUNTIME) that must be set before
// the worker process even starts.
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);

builder.ConfigureFunctionsWebApplication();

// ChatBot.Data's tokenizer/model/training/generation/chat services - the same set
// ChatBot.Api registers - so training here behaves identically to training in-process.
builder.Services.AddChatBotDataServices();

builder.Services.Configure<BlobStorageOptions>(builder.Configuration.GetSection("AzureStorage"));
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();

// Reports training progress/completion straight to ChatBot.Api over HTTP instead of a
// status Service Bus queue - see TrainingStatusReporter and TrainingCallbackOptions.
builder.Services.Configure<TrainingCallbackOptions>(builder.Configuration.GetSection("TrainingCallback"));
builder.Services.AddHttpClient<ChatBot.Train.ITrainingStatusReporter, ChatBot.Train.TrainingStatusReporter>();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

builder.Build().Run();
