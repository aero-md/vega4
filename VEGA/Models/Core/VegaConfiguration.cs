namespace Models.Core;

public class VegaConfiguration
{
    public VegaConfiguration(string botToken, string dbConnexionString, List<ulong>? superAdminUserIds = null)
    {
        BotToken = botToken;
        DbConnexionString = dbConnexionString;
        SuperAdminUserIds = superAdminUserIds ?? new List<ulong>();
    }
    
    public string BotToken { get; set; }
    public string DbConnexionString { get; set; }
    public List<ulong> SuperAdminUserIds { get; set; }
}