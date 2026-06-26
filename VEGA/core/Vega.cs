using static Core.GlobalRegistry;
using System.Reflection;
using NetCord;
using NetCord.Gateway;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;
using NetCord.Logging;
using Handlers;

namespace Core;

public class Vega
{
    // Client is created during Initialize; use null-forgiving here and assign in configureGatewayClient
    private ShardedGatewayClient ShardedClient { get; set; } = null!;
    // Initialize the command service so the property is non-null after construction
    private ApplicationCommandService<ApplicationCommandContext> ApplicationCommandService { get; set; } = null!;
    private ComponentInteractionService<ButtonInteractionContext> ButtonInteractionService { get; set; } = null!;
    private ComponentInteractionService<StringMenuInteractionContext> StringMenuInteractionService { get; set; } = null!;
    private ComponentInteractionService<ModalInteractionContext> ModalInteractionService { get; set; } = null!;
    
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

        // Discover [ComponentInteraction] modules in this assembly. No remote
        // registration needed — Discord identifies handlers by customId at click
        // time, not at startup like slash commands.
        ButtonInteractionService = new ComponentInteractionService<ButtonInteractionContext>();
        ButtonInteractionService.AddModules(Assembly.GetExecutingAssembly());

        // String select menus get their own service+context (AddModules only picks up
        // modules whose context matches). Routed alongside buttons in InteractionCreate.
        StringMenuInteractionService = new ComponentInteractionService<StringMenuInteractionContext>();
        StringMenuInteractionService.AddModules(Assembly.GetExecutingAssembly());

        // Modal submissions (e.g. /trigger add) get their own service+context.
        ModalInteractionService = new ComponentInteractionService<ModalInteractionContext>();
        ModalInteractionService.AddModules(Assembly.GetExecutingAssembly());

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

                // String select menus (more specific than MessageComponentInteraction, so listed first)
                case StringMenuInteraction stringMenuInteraction:
                    await ComponentInteractionHandler.HandleAsync(client, StringMenuInteractionService, stringMenuInteraction);
                    break;

                // Component Interaction (button clicks, etc.)
                case MessageComponentInteraction componentInteraction:
                    await ComponentInteractionHandler.HandleAsync(client, ButtonInteractionService, componentInteraction);
                    break;

                // Modal submissions (e.g. /trigger add form)
                case ModalInteraction modalInteraction:
                    await ComponentInteractionHandler.HandleAsync(client, ModalInteractionService, modalInteraction);
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