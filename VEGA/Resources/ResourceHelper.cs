using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Models;
using NetCord;
using NetCord.Services;

namespace Resources;

/// <summary>
/// Helper class to access resource strings in a simple, direct and concise way
/// </summary>
public static class ResourceHelper
{
    private const string MISSING_RESSOURCE_MESSAGE = "Missing Resource";
    // Concurrent: GetString runs on gateway threads AND background threads (PollService timer,
    // FeedWidgetButtons.RefreshListAsync). A plain Dictionary written from several threads can
    // corrupt its internal state (100% CPU spin / IndexOutOfRangeException).
    private static readonly ConcurrentDictionary<string, JsonDocument> _cachedDocuments = new();
    private static readonly ConcurrentDictionary<(string language, string path), string> _stringCache = new();

    static ResourceHelper()
    {
        foreach (string lang in Language.SupportedLanguages)
        {
            _cachedDocuments[lang] = LoadMessagesJson(lang);
        }
    }

    private static JsonDocument LoadMessagesJson(string language)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"VEGA.Resources.{language}.res.json";
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            
            if (stream == null)
                return JsonDocument.Parse("{}");

            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            
            return JsonDocument.Parse(json);
        }
        catch
        {
            return JsonDocument.Parse("{}");
        }
    }

    /// <summary>
    /// Generic method to retrieve any message by hierarchical path
    /// Example: GetMessage("exceptions.missingPermission") returns the exceptions.missingPermission string
    /// </summary>
    public static string GetString(string path, string userLocale, params object[] args)
    {
        string language = Language.GetLanguageFromLocale(userLocale);
        var cacheKey = (language, path);
        
        // Vérifier le cache à deux niveaux
        if (_stringCache.TryGetValue(cacheKey, out var cachedResult))
            return args.Length > 0 ? string.Format(cachedResult, args) : cachedResult;
        
        string result = RetrieveStringFromJson(path, language);
        
        // Mettre en cache le résultat brut (sans formatting)
        if (result != MISSING_RESSOURCE_MESSAGE)
            _stringCache[cacheKey] = result;
        
        // Formater avec les arguments si nécessaire
        return args.Length > 0 ? string.Format(result, args) : result;
    }

    private static string RetrieveStringFromJson(string path, string language)
    {
        // Obtenir le document JSON pour la langue (ou fallback à la langue par défaut)
        if (!_cachedDocuments.TryGetValue(language, out var languageDocument))
        {
            if (!_cachedDocuments.TryGetValue(Language.DefaultLanguage, out languageDocument))
                return MISSING_RESSOURCE_MESSAGE;
        }
        
        JsonElement root = languageDocument.RootElement;
        
        // Traverser la structure JSON en fonction des parties du chemin
        var parts = path.Split('.');
        JsonElement current = root;
        
        foreach (var part in parts)
        {
            if (!current.TryGetProperty(part, out var property))
                return MISSING_RESSOURCE_MESSAGE;
            
            current = property;
        }
        
        // Vérifier que le résultat est une chaîne de caractères
        return current.ValueKind == JsonValueKind.String 
            ? current.GetString() ?? MISSING_RESSOURCE_MESSAGE 
            : MISSING_RESSOURCE_MESSAGE;
    }
}
