using GitUpdater.GitProviders;
using GitUpdater.Services;
using OpenTelemetry.Trace;
using Serilog;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;

namespace GitUpdater;

public class Program
{
    public static void Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
                .Build())
            .CreateLogger();

        try
        {
            Log.Information("Starting GitUpdater");

            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog();
            builder.AddServiceDefaults();

            // Add Redis via Aspire
            builder.AddRedisClient("QueueManager");

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // Register Git providers
            builder.Services.AddSingleton<IGitProvider, GenericGitProvider>();
            builder.Services.AddSingleton<IGitProvider, AzureReposGitProvider>();
            builder.Services.AddSingleton<IGitProvider, GitHubGitProvider>();
            builder.Services.AddSingleton<IGitProvider, GitLabGitProvider>();
            builder.Services.AddSingleton<IGitProvider, BitbucketGitProvider>();
            builder.Services.AddSingleton<GitProviderFactory>();

            // Register queue services
            builder.Services.AddSingleton<RedisQueueService>();
            builder.Services.AddHostedService<QueueProcessorService>();

            // Add OpenTelemetry tracing for Redis and queue processor
            builder.Services.AddOpenTelemetry()
                .WithTracing(tracing =>
                {
                    tracing.AddSource("GitUpdater.QueueProcessor");
                    tracing.AddRedisInstrumentation();
                });

            var app = builder.Build();

            app.MapDefaultEndpoints();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();

            app.MapControllers();

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
    }
}
