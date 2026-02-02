using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Core;
using Models.Core;
using Handlers;
using Microsoft.Extensions.Caching.Memory;
using Services;

// Configuration
IConfiguration appSettings = new ConfigurationBuilder()
                                    .AddJsonFile("appsettings.json")
                                    .Build();

VegaConfiguration configuration = new VegaConfiguration
(
    appSettings.GetValue<string>("botToken") ?? throw new Exception("token not found"),
    appSettings.GetSection("postgres").GetValue<string>("connexionString") ?? throw new Exception("postgres connexion string not found"),
    appSettings.GetValue<List<ulong>>("superAdminUserIds") // possibly null
);

// Build DI container
var serviceProvider = new ServiceCollection()
                            // Singleton
                            .AddSingleton(configuration)
                            .AddSingleton<IMemoryCache, MemoryCache>()
                            .AddSingleton<Vega>()
                            // Feeds service
                            .AddSingleton<FeedService>()
                            // Scoped
                            .AddScoped<AppDbContext>()
                            .AddScoped<GuildSettingsService>()
                            // Transient
                            .AddTransient<WaifuApiService>()
                            // Logging
                            .AddLogging()
                            .BuildServiceProvider();
  
// Expose provider via ServiceRegistry in Core namespace for parts that are not created via DI
GlobalRegistry.SetMainServiceProvider(serviceProvider);


// Resolve Vega instance from DI
var vega = serviceProvider.GetRequiredService<Vega>();
// Init and launch
await vega.Initialize(configuration.BotToken);
await vega.Launch();