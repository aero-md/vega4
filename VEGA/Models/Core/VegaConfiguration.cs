namespace Models.Core;

public class VegaConfiguration
{
    public VegaConfiguration(
        string botToken, 
        string dbConnexionString, 
        List<ulong>? superAdminUserIds = null,
        ulong? backofficeGuildId = null)
    {
        BotToken = botToken;
        DbConnexionString = dbConnexionString;
        SuperAdminUserIds = superAdminUserIds ?? new List<ulong>();
        BackofficeGuildId = backofficeGuildId;
    }
    
    public string BotToken { get; set; }
    public string DbConnexionString { get; set; }
    public List<ulong> SuperAdminUserIds { get; set; }
    public ulong? BackofficeGuildId { get; set; }
}