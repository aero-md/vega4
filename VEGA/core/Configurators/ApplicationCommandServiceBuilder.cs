using System.Reflection;
using Core;
using Core.CustomCommandAttributes;
using Microsoft.Extensions.DependencyInjection;
using Models.Core;
using NetCord.Gateway;
using NetCord.Services.ApplicationCommands;

namespace Core.Configurators
{
    /// <summary>
    /// Small fluent builder to create and configure a GatewayClient together with an ApplicationCommandService.
    /// </summary>
    public class ApplicationCommandServiceBuilder
    {
        private ApplicationCommandService<ApplicationCommandContext> _appCommandService;

        private ApplicationCommandServiceBuilder()
        {
            _appCommandService = new ApplicationCommandService<ApplicationCommandContext>();
        }

        public static ApplicationCommandServiceBuilder Create()
        {
            return new ApplicationCommandServiceBuilder();
        }

        public ApplicationCommandServiceBuilder DiscoverCommands()
        {
            var commandModules = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => (t.Namespace == "SlashCommands" || t.Namespace == "UserCommands" || t.Namespace == "MessageCommands")
                        && t.IsClass
                        && !t.IsAbstract
                        && typeof(ApplicationCommandModule<ApplicationCommandContext>).IsAssignableFrom(t));

            foreach (var moduleType in commandModules)
            {
                _appCommandService.AddModule(moduleType);
            }

            // Add any default configuration here if needed
            return this;
        }

        public async Task<ApplicationCommandService<ApplicationCommandContext>> BuildAsync(ShardedGatewayClient client)
        {
            var config = GlobalRegistry.MainServiceProvider.GetRequiredService<VegaConfiguration>();

            // Separate regular commands from backoffice commands
            var allCommandTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => (t.Namespace == "SlashCommands" || t.Namespace == "UserCommands" || t.Namespace == "MessageCommands")
                        && t.IsClass
                        && !t.IsAbstract
                        && typeof(ApplicationCommandModule<ApplicationCommandContext>).IsAssignableFrom(t))
                .ToList();

            var backofficeTypes = allCommandTypes
                .Where(t => t.GetCustomAttribute<BackofficeCommandAttribute>() != null)
                .ToList();

            var regularTypes = allCommandTypes
                .Except(backofficeTypes)
                .ToList();

            // Register regular commands globally
            if (regularTypes.Any())
            {
                var regularCommandService = new ApplicationCommandService<ApplicationCommandContext>();
                foreach (var moduleType in regularTypes)
                {
                    regularCommandService.AddModule(moduleType);
                }
                await regularCommandService.RegisterCommandsAsync(client.Rest, client.Id);
            }

            // Register backoffice commands only on backoffice guild if configured
            if (backofficeTypes.Any() && config.BackofficeGuildId.HasValue)
            {
                var backofficeCommandService = new ApplicationCommandService<ApplicationCommandContext>();
                foreach (var moduleType in backofficeTypes)
                {
                    backofficeCommandService.AddModule(moduleType);
                }
                await backofficeCommandService.RegisterCommandsAsync(
                    client.Rest, 
                    client.Id, 
                    config.BackofficeGuildId.Value
                );
            }
            
            return _appCommandService;
        }
    }
}
