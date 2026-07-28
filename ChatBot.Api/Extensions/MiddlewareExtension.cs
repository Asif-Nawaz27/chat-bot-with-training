using Microsoft.AspNetCore.Hosting.Server;
using ChatBot.Api.Services;
using ChatBot.Data;
using ChatBot.Data.Messaging;
using ChatBot.Data.Storage;

namespace ChatBot.Api.Extensions
{
    public static class MiddlewareExtension
    {


        public static void RegisterServices(this IServiceCollection services)
        {
            // Registers the ChatBot.Data library's tokenizer/model/training/generation/chat
            // services (see ChatBot.Data/ServiceRegistration.cs) - the exact same set the
            // console app (ChatBot.Cli) uses, so both entry points behave identically.
            services.AddChatBotDataServices();

            // API-only: runs training as a background job so HTTP callers can poll for
            // progress instead of blocking a request for several minutes.
            services.AddSingleton<ITrainingJobService, TrainingJobService>();

            // API-only: optionally syncs the local Data/ folder with Azure Blob Storage
            services.AddSingleton<IBlobStorageService, BlobStorageService>();

            // Publishes training job requests to Service Bus for ChatBot.Train to pick up,
            // and listens on the status queue for the progress/completion messages it sends back.
            services.AddSingleton<IServiceBusPublisher, ServiceBusPublisher>();
            services.AddHostedService<TrainingStatusListener>();

            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            services.AddOpenApi();
        }



        public static void GetConfiguration(this IServiceCollection services, ConfigurationManager configuration)
        {

            // ("data" and "checkpoint" containers) - see appsettings.Development.json.
            // Disabled (a no-op) when no AzureStorage:ConnectionString is configured.
            services.Configure<BlobStorageOptions>(configuration.GetSection("AzureStorage"));

            // Service Bus queue names/connection string used to hand training jobs off to
            // ChatBot.Train and receive progress back - see appsettings.Development.json.
            services.Configure<ServiceBusOptions>(configuration.GetSection("AzureServiceBus"));

            // CORS allowed origins - see appsettings.json's "AppSetting:cors".
            services.Configure<AppSettingOptions>(configuration.GetSection("AppSetting"));
        }



        public static void AddCorePolicy(this IServiceCollection services, ConfigurationManager configuration, string WebClientCorsPolicy)
        {
            // Allows the ChatBot.Web React app(served from Vite's dev server on a
            // different port) to call this API from the browser. Origins come from
            // AppSetting:cors in appsettings.json as a comma-separated list.
            var corsOrigins = (configuration["AppSetting:cors"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            services.AddCors(options =>
            {
                options.AddPolicy(WebClientCorsPolicy, policy =>
                {
                    policy.WithOrigins(corsOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                });
            });
        }


        public async static void setServicesScop(this IServiceProvider service)
        {
            // If Azure Blob Storage is configured, make sure the "data"/"checkpoint" containers
            // exist, pull down a previously trained checkpoint (if any) so this instance can
            // chat immediately without retraining, and seed the built-in sample dataset into
            // blob storage the first time. All of this is a no-op when it isn't configured.
            using (var startupScope = service.CreateScope())
            {
                var blobStorage = startupScope.ServiceProvider.GetRequiredService<IBlobStorageService>();
                if (blobStorage.IsEnabled)
                {
                    await blobStorage.EnsureContainersExistAsync();

                    var dataDirectory = RepoPaths.DataDirectory;
                    await blobStorage.TryDownloadCheckpointFileAsync("model.dat", Path.Combine(dataDirectory, "model.dat"));
                    await blobStorage.TryDownloadCheckpointFileAsync("vocab.json", Path.Combine(dataDirectory, "vocab.json"));
                    await blobStorage.TryDownloadCheckpointFileAsync("model_config.json", Path.Combine(dataDirectory, "model_config.json"));

                    var sampleDatasetPath = Path.Combine(dataDirectory, "sample_conversations.txt");
                    if (File.Exists(sampleDatasetPath) && !await blobStorage.DatasetExistsAsync("sample_conversations.txt"))
                    {
                        await blobStorage.UploadDatasetAsync("sample_conversations.txt", sampleDatasetPath);
                    }
                }
            }
        }


    }
}
