using Models.Core;

namespace VEGA.Tests.EndToEnd;

public class TestConfiguration
{
    public string BotToken { get; set; } = "";
    public ulong TestGuildId { get; set; }
    public ulong TestChannelId { get; set; }
    public string DbConnectionString { get; set; } = "";
    public List<ulong> SuperAdminUserIds { get; set; } = new();
    public ulong? BackofficeGuildId { get; set; }

    public VegaConfiguration ToVegaConfiguration()
    {
        return new VegaConfiguration(BotToken, DbConnectionString, SuperAdminUserIds, BackofficeGuildId);
    }
}
