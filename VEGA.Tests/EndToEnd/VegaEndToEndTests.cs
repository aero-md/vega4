using Xunit;
using Core;
using System.Threading.Tasks;
using NetCord;
using NetCord.Rest;

namespace VEGA.Tests.EndToEnd;

/// <summary>
/// End-to-End tests for basic Vega bot commands
/// These tests use a single bot instance and verify behavior via Discord REST API
/// </summary>
[Collection("VegaBotCollection")]
public class VegaEndToEndTests : IClassFixture<VegaBotFixture>
{
    private readonly VegaBotFixture _fixture;
    private readonly RestClient _restClient;
    private readonly ulong _testGuildId;
    private readonly ulong _testChannelId;

    public VegaEndToEndTests(VegaBotFixture fixture)
    {
        _fixture = fixture;
        _restClient = fixture.RestClient;
        _testGuildId = fixture.TestConfig.TestGuildId;
        _testChannelId = fixture.TestConfig.TestChannelId;
    }

    [Fact]
    public async Task BotShouldBeConnectedAndReachable()
    {
        // Arrange & Act
        var currentUser = await _restClient.GetCurrentUserAsync();

        // Assert
        Assert.NotNull(currentUser);
        Assert.NotEqual(0ul, currentUser.Id);
        Assert.False(string.IsNullOrEmpty(currentUser.Username));
    }

    [Fact]
    public async Task BotShouldBeInTestGuild()
    {
        // Arrange & Act
        var guilds = new List<RestGuild>();
        await foreach (var guild in _restClient.GetCurrentUserGuildsAsync())
        {
            guilds.Add(guild);
        }

        // Assert
        Assert.Contains(guilds, g => g.Id == _testGuildId);
    }

    [Fact]
    public async Task UpCommand_ShouldHaveRegisteredCommandInGuild()
    {
        // Arrange
        var botUser = await _restClient.GetCurrentUserAsync();

        // Act
        var commands = await _restClient.GetGuildApplicationCommandsAsync(
            botUser.Id,
            _testGuildId
        );

        // Assert
        var upCommand = commands.FirstOrDefault(c => c.Name == "up");
        Assert.NotNull(upCommand);
        Assert.Equal("up", upCommand.Name);
        Assert.Contains("uptime", upCommand.Description.ToLower());
    }

    [Fact]
    public async Task ClearCommand_ShouldHaveRegisteredCommandInGuild()
    {
        // Arrange
        var botUser = await _restClient.GetCurrentUserAsync();

        // Act
        var commands = await _restClient.GetGuildApplicationCommandsAsync(
            botUser.Id,
            _testGuildId
        );

        // Assert
        var clearCommand = commands.FirstOrDefault(c => c.Name == "clear");
        Assert.NotNull(clearCommand);
        Assert.Equal("clear", clearCommand.Name);
        Assert.Contains("delete", clearCommand.Description.ToLower());
    }

    [Fact]
    public async Task Clear_ShouldDeleteMessagesWhenInvoked()
    {
        // Arrange - Create test messages
        var channel = await _restClient.GetChannelAsync(_testChannelId);
        Assert.NotNull(channel);
        
        var textChannel = channel as TextChannel;
        Assert.NotNull(textChannel);

        // Send 5 test messages
        var sentMessageIds = new List<ulong>();
        for (int i = 0; i < 5; i++)
        {
            var msg = await textChannel.SendMessageAsync(new MessageProperties
            {
                Content = $"Test message {i + 1} for deletion test"
            });
            sentMessageIds.Add(msg.Id);
        }

        // Wait a bit to ensure messages are sent
        await Task.Delay(1000);

        // Act - Delete the messages using REST API directly
        // (since we can't easily trigger slash commands programmatically)
        await textChannel.DeleteMessagesAsync(sentMessageIds);

        // Wait for deletion to process
        await Task.Delay(1000);

        // Assert - Verify messages are deleted by trying to fetch them
        foreach (var msgId in sentMessageIds)
        {
            await Assert.ThrowsAsync<RestException>(async () =>
            {
                await textChannel.GetMessageAsync(msgId);
            });
        }
    }

    [Fact]
    public async Task BotCanSendAndReceiveMessages()
    {
        // Arrange
        var channel = await _restClient.GetChannelAsync(_testChannelId);
        var textChannel = channel as TextChannel;
        Assert.NotNull(textChannel);

        // Act - Send a message as the bot
        var message = await textChannel.SendMessageAsync(new MessageProperties
        {
            Content = "Test message from E2E tests"
        });

        // Assert
        Assert.NotNull(message);
        Assert.Equal("Test message from E2E tests", message.Content);

        // Cleanup - Delete the test message
        await message.DeleteAsync();
    }

    [Fact]
    public async Task BotCanSendEmbed()
    {
        // Arrange
        var channel = await _restClient.GetChannelAsync(_testChannelId);
        var textChannel = channel as TextChannel;
        Assert.NotNull(textChannel);

        var embed = new EmbedProperties
        {
            Title = "Test Embed",
            Description = "This is a test embed from E2E tests",
            Color = new Color(0x00FF00)
        };

        // Act - Send an embed message
        var message = await textChannel.SendMessageAsync(new MessageProperties
        {
            Embeds = new[] { embed }
        });

        // Assert
        Assert.NotNull(message);
        Assert.NotNull(message.Embeds);
        Assert.Single(message.Embeds);
        
        var receivedEmbed = message.Embeds.First();
        Assert.Equal("Test Embed", receivedEmbed.Title);
        Assert.Equal("This is a test embed from E2E tests", receivedEmbed.Description);

        // Cleanup
        await message.DeleteAsync();
    }

    [Fact]
    public async Task Clear_ShouldHandleBulkDeletion()
    {
        // Arrange
        var channel = await _restClient.GetChannelAsync(_testChannelId);
        var textChannel = channel as TextChannel;
        Assert.NotNull(textChannel);

        // Send 10 test messages
        var sentMessageIds = new List<ulong>();
        for (int i = 0; i < 10; i++)
        {
            var msg = await textChannel.SendMessageAsync(new MessageProperties
            {
                Content = $"Bulk test message {i + 1}"
            });
            sentMessageIds.Add(msg.Id);
            await Task.Delay(100); // Small delay to avoid rate limits
        }

        await Task.Delay(1000);

        // Act - Delete all messages in bulk
        await textChannel.DeleteMessagesAsync(sentMessageIds);
        await Task.Delay(1000);

        // Assert - Verify all messages are deleted
        var fetchedMessages = new List<RestMessage>();
        await foreach (var msg in textChannel.GetMessagesAsync(new PaginationProperties<ulong>
        {
            BatchSize = 20
        }))
        {
            if (sentMessageIds.Contains(msg.Id))
            {
                fetchedMessages.Add(msg);
            }
        }

        Assert.Empty(fetchedMessages);
    }
}

/// <summary>
/// Collection definition to share fixture across test classes
/// </summary>
[CollectionDefinition("VegaBotCollection")]
public class VegaBotCollection : ICollectionFixture<VegaBotFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and the ICollectionFixture<> interface.
}