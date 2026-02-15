using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Core;
using NetCord;
using NetCord.Rest;
using Services;
using Microsoft.Extensions.Caching.Memory;

namespace VEGA.Tests.EndToEnd;

/// <summary>
/// Fixture that creates a single bot instance for all end-to-end tests
/// </summary>
public class VegaBotFixture : IAsyncLifetime
{
    public TestConfiguration TestConfig { get; private set; } = null!;
    public RestClient RestClient => _vega!.Rest;
    public IServiceProvider ServiceProvider { get; private set; } = null!;
    
    private Vega? _vega;
    private CancellationTokenSource? _botCts;

    public async Task InitializeAsync()
    {
        // Load test configuration
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.test.json", optional: false);

        var config = configBuilder.Build();

        TestConfig = new TestConfiguration
        {
            BotToken = config.GetValue<string>("botToken") ?? throw new Exception("Bot token not found in test config"),
            TestGuildId = config.GetValue<ulong>("testGuildId"),
            TestChannelId = config.GetValue<ulong>("testChannelId"),
            DbConnectionString = config.GetSection("postgres").GetValue<string>("connexionString") ?? throw new Exception("DB connection string not found"),
            SuperAdminUserIds = config.GetValue<List<ulong>?>("superAdminUserIds") ?? new List<ulong>(),
            BackofficeGuildId = config.GetValue<ulong?>("backofficeGuildId")
        };

        // Build DI container for the bot
        var vegaConfig = TestConfig.ToVegaConfiguration();
        
        ServiceProvider = new ServiceCollection()
            .AddSingleton(vegaConfig)
            .AddSingleton<IMemoryCache, MemoryCache>()
            .AddSingleton<Vega>()
            .AddSingleton<FeedService>()
            .AddSingleton<ReminderService>()
            .AddSingleton<RestClient>(provider => provider.GetRequiredService<Vega>().Rest)
            .AddScoped<AppDbContext>()
            .AddScoped<GuildSettingsService>()
            .AddTransient<WaifuApiService>()
            .AddLogging()
            .BuildServiceProvider();

        GlobalRegistry.SetMainServiceProvider(ServiceProvider);

        // Resolve and initialize Vega
        _vega = ServiceProvider.GetRequiredService<Vega>();
        await _vega.Initialize(TestConfig.BotToken);

        // Start bot in background
        _botCts = new CancellationTokenSource();
        _ = Task.Run(async () => 
        {
            try
            {
                await _vega.Launch();
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
        }, _botCts.Token);

        // Wait a bit for bot to connect
        await Task.Delay(5000);
    }

    public async Task DisposeAsync()
    {
        _botCts?.Cancel();
        _botCts?.Dispose();
        
        if (ServiceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }

        await Task.CompletedTask;
    }
}
