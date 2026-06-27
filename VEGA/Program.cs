using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Core;
using Models.Core;
using Handlers;
using Microsoft.Extensions.Caching.Memory;
using Services;
using Serilog;
using Polly;
using Polly.Extensions.Http;

// VEGA's schema uses `timestamp without time zone` columns (see database/migrations).
// Opt into Npgsql's legacy timestamp behavior so EF maps DateTime -> `timestamp` (not
// `timestamptz`) and tolerates Kind=Unspecified values read back from those columns.
// Must run before any Npgsql type mapping is resolved.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Configuration
IConfiguration appSettings = new ConfigurationBuilder()
                                    .AddJsonFile("appsettings.json")
                                    .Build();

// Configure Serilog
var logPath = appSettings.GetSection("logging").GetValue<string>("logPath") ?? "./logs/vega-.log";
var retainedDays = appSettings.GetSection("logging").GetValue<int?>("retainedDays") ?? 30;
var fileSizeLimit = appSettings.GetSection("logging").GetValue<long?>("fileSizeLimitBytes") ?? 5_000_000;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: logPath,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: retainedDays,
        fileSizeLimitBytes: fileSizeLimit,
        rollOnFileSizeLimit: true,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
    )
    .CreateLogger();

VegaConfiguration configuration = new VegaConfiguration
(
    appSettings.GetValue<string>("botToken") ?? throw new Exception("token not found"),
    appSettings.GetSection("postgres").GetValue<string>("connexionString") ?? throw new Exception("postgres connexion string not found"),
    appSettings.GetSection("superAdminUserIds").Get<List<ulong>>(), // GetValue doesn't work for collections
    appSettings.GetValue<ulong?>("backofficeGuildId") // possibly null
);

// Retry policy for HTTP requests (exponential backoff)
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// Build DI container
var serviceProvider = new ServiceCollection()
                            // Singleton
                            .AddSingleton(configuration)
                            .AddSingleton<IMemoryCache, MemoryCache>()
                            .AddSingleton<Vega>()
                            // HttpClientFactory with Polly retry for Reddit API
                            .AddHttpClient(HttpClientNames.Reddit, client =>
                            {
                                // Reddit rejects generic UAs with 403. Required format:
                                // <platform>:<app-id>:<version> (by /u/<reddit-username>)
                                client.DefaultRequestHeaders.Add("User-Agent", HttpClientNames.RedditUserAgent);
                            })
                            .AddPolicyHandler(retryPolicy)
                            .Services
                            .AddHttpClient(HttpClientNames.AnimeImages)
                            .AddPolicyHandler(retryPolicy)
                            .Services
                            .AddHttpClient(HttpClientNames.Emotes, client =>
                            {
                                client.MaxResponseContentBufferSize = HttpClientNames.EmoteMaxResponseBytes;
                            })
                            .Services
                            // Feeds services
                            .AddSingleton<FeedContentService>()
                            .AddSingleton<FeedService>()
                            // Poll service
                            .AddSingleton<PollService>()
                            // Reminder service
                            //.AddSingleton<ReminderService>()
                            // Scoped
                            .AddScoped<AppDbContext>()
                            .AddScoped<GuildSettingsService>()
                            // Transient
                            .AddTransient<WaifuApiService>()
                            // Logging with Serilog
                            .AddLogging(builder => builder.AddSerilog(dispose: true))
                            .BuildServiceProvider();
  
// Expose provider via ServiceRegistry in Core namespace for parts that are not created via DI
GlobalRegistry.SetMainServiceProvider(serviceProvider);


// Resolve Vega instance from DI
var vega = serviceProvider.GetRequiredService<Vega>();
// Init and launch
await vega.Initialize(configuration.BotToken);

// Initialize FeedService now that Vega (and its RestClient) is ready
var feedService = serviceProvider.GetRequiredService<FeedService>();
await feedService.Initialize(vega.Rest);

// Initialize PollService now that Vega (and its RestClient) is ready
var pollService = serviceProvider.GetRequiredService<PollService>();
await pollService.Initialize(vega.Rest);

// Initialize ReminderService now that Vega (and its RestClient) is ready
//var reminderService = serviceProvider.GetRequiredService<ReminderService>();
//await reminderService.Initialize(vega.Rest);

await vega.Launch();