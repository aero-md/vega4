using static Core.GlobalRegistry;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Logging;
using Handlers;

namespace Core;

public class Vega
{
    // Client is created during Initialize; use null-forgiving here and assign in configureGatewayClient
    private ShardedGatewayClient ShardedClient { get; set; } = null!;
    // Initialize the command service so the property is non-null after construction
    private ApplicationCommandService<ApplicationCommandContext> ApplicationCommandService { get; set; } = null!;
    
    // Public access to the RestClient from ShardedGatewayClient
    public RestClient Rest => ShardedClient.Rest;

    # region Constructor and Initializing

    public Vega(){}

    public async Task Initialize(string botToken)
    {
        // Use the fluent GatewayClientBuilder to create and configure the client and command service
        ShardedClient = new ShardedGatewayClient
        (
            new BotToken(botToken), new ShardedGatewayClientConfiguration
            {
                IntentsFactory = (shard) => GatewayIntents.GuildMessages | GatewayIntents.DirectMessages | GatewayIntents.MessageContent | GatewayIntents.Guilds | GatewayIntents.GuildUsers,
                LoggerFactory = ShardedConsoleLogger.GetFactory()
            }
        );

        // Configure all registered handlers
        // Register all commands to Discord
        ApplicationCommandService = await Configurators.ApplicationCommandServiceBuilder
                                                        .Create()
                                                        .DiscoverCommands()
                                                        .BuildAsync(ShardedClient);

        // Misc Handlers (static)
        ShardedClient.Connecting += (client) =>
        {
            MiscHandlers.Connecting(client);
            return ValueTask.CompletedTask;
        };
        ShardedClient.Connect += async (client) => await MiscHandlers.Connected(client);

        // Message Create (singleton)
        ShardedClient.MessageCreate += async (client, message) => await MessageCreateHandler.MessageCreate(client, message);

        // InteractionCommand Create (singleton)
        ShardedClient.InteractionCreate += async (client, interaction) =>
        {
            switch (interaction)
            {
                // Command Interaction
                case ApplicationCommandInteraction cmdInteraction:
                    await CommandInteractionHandler.HandleCommand(client, ApplicationCommandService, cmdInteraction);
                    break;

                // Unsupported interaction type
                default:
                    return;
            }
        };
    }

    public async Task Launch()
    {
        await ShardedClient.StartAsync();
        await Task.Delay(-1);
    }

    # endregion

    // Clear local ApplicationCommandService (remove all local commands)
    public void ClearLocalCommands()
    {
        // Recreate the service to remove registered commands and modules
        ApplicationCommandService = new ApplicationCommandService<ApplicationCommandContext>();
    }

    // Clear all commands on Discord (global or for a specific guild) and terminate program
    public async Task ClearAllRegisteredCommandsAsync(ulong? guildId = null)
    {
        var empty = Array.Empty<ApplicationCommandProperties>();
        if (guildId.HasValue)
            await ShardedClient.Rest.BulkOverwriteGuildApplicationCommandsAsync(ShardedClient.Id, guildId.Value, empty);
        else
            await ShardedClient.Rest.BulkOverwriteGlobalApplicationCommandsAsync(ShardedClient.Id, empty);
    }
}