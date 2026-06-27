namespace Models;

public class Language
{
    // Language codes
    public const string ENGLISH = "eng";
    public const string FRENCH = "fr";

    // Default language getter
    public static string DefaultLanguage
    {
        get {
            return ENGLISH;
        }
    }

    // Supported languages getter
    public static string[] SupportedLanguages {
        get { 
            return [ENGLISH, FRENCH]; 
        }
    }

    public static string GetLanguageFromLocale(string locale)
    {
        return locale switch
        {
            "fr" or "fr-FR" => FRENCH,
            _ => ENGLISH,
        };
    }
}


/// <summary>
/// Discord supported user locales
/// </summary>
public enum UserLocale
{
    /// <summary>Indonesian - Bahasa Indonesia</summary>
    ID,
    
    /// <summary>Danish - Dansk</summary>
    DA,
    
    /// <summary>German - Deutsch</summary>
    DE,
    
    /// <summary>English, UK - English, UK</summary>
    EN_GB,
    
    /// <summary>English, US - English, US</summary>
    EN_US,
    
    /// <summary>Spanish - Español</summary>
    ES_ES,
    
    /// <summary>Spanish, LATAM - Español, LATAM</summary>
    ES_419,
    
    /// <summary>French - Français</summary>
    FR,
    
    /// <summary>Croatian - Hrvatski</summary>
    HR,
    
    /// <summary>Italian - Italiano</summary>
    IT,
    
    /// <summary>Lithuanian - Lietuviškai</summary>
    LT,
    
    /// <summary>Hungarian - Magyar</summary>
    HU,
    
    /// <summary>Dutch - Nederlands</summary>
    NL,
    
    /// <summary>Norwegian - Norsk</summary>
    NO,
    
    /// <summary>Polish - Polski</summary>
    PL,
    
    /// <summary>Portuguese, Brazilian - Português do Brasil</summary>
    PT_BR,
    
    /// <summary>Romanian, Romania - Română</summary>
    RO,
    
    /// <summary>Finnish - Suomi</summary>
    FI,
    
    /// <summary>Swedish - Svenska</summary>
    SV_SE,
    
    /// <summary>Vietnamese - Tiếng Việt</summary>
    VI,
    
    /// <summary>Turkish - Türkçe</summary>
    TR,
    
    /// <summary>Czech - Čeština</summary>
    CS,
    
    /// <summary>Greek - Ελληνικά</summary>
    EL,
    
    /// <summary>Bulgarian - български</summary>
    BG,
    
    /// <summary>Russian - Pусский</summary>
    RU,
    
    /// <summary>Ukrainian - Українська</summary>
    UK,
    
    /// <summary>Hindi - हिन्दी</summary>
    HI,
    
    /// <summary>Thai - ไทย</summary>
    TH,
    
    /// <summary>Chinese, China - 中文</summary>
    ZH_CN,
    
    /// <summary>Japanese - 日本語</summary>
    JA,
    
    /// <summary>Chinese, Taiwan - 繁體中文</summary>
    ZH_TW,
    
    /// <summary>Korean - 한국어</summary>
    KO
}
