# Audit sécurité — VEGA4

> Généré le 2026-04-30. État du code analysé : `MessageCreateHandler`, `FeedService`, `FeedContentService`, `WaifuApiService`, `GuildSettingsService`, `Triggers`, `Feeds`, `ClearMsgs`, `DownloadEmotes`, `ComponentInteractionHandler`, `Program.cs`.

Les éléments déjà couverts dans `FEED_ANALYSIS.md` (validation subreddit, `IServiceScopeFactory`, `IHttpClientFactory`, désactivation feed sur 403/404, NSFW filtering) ont été corrigés et **ne sont pas repris ici**. Cette liste cible ce qui reste à durcir.

---

## 🔴 Critique

### 1. ReDoS sur les triggers utilisateur — pas de timeout regex ✅ Corrigé (2026-05-01)
**Fichier :** `Handlers/MessageCreateHandler.cs:56`

```csharp
if (Regex.IsMatch(msg.Content, pattern.Pattern, (RegexOptions)pattern.RegexOptions))
```

Le pattern provient d'un utilisateur Discord (modérateur du serveur, mais non-trusted). Aucun timeout n'est passé : un pattern catastrophique du type `(a+)+b` ou `(a|a)*b` sur un message long bloque le thread du gateway pendant plusieurs secondes. Comme **chaque message** d'un guild itère sur **tous** ses triggers, un seul pattern hostile suffit à geler la modération du serveur.

**Correction :** passer un `TimeSpan.FromMilliseconds(100)` à `Regex.IsMatch` (ou utiliser une instance `Regex` cachée avec `MatchTimeout`), et valider également côté création (`Triggers.Add`) en compilant une fois le pattern avec ce timeout pour rejeter immédiatement les ReDoS évidents.

---

### 2. Catch silencieux qui masque les erreurs regex ✅ Corrigé (2026-05-01)
**Fichier :** `Handlers/MessageCreateHandler.cs:62-65`

```csharp
catch (Exception)
{
    
}
```

Combiné au point 1, un `RegexMatchTimeoutException` ou `ArgumentException` (pattern invalide) est avalé. Conséquences :
- Un trigger cassé tourne en exception sur **chaque message** sans alerte.
- Un attaquant ne laisse aucune trace dans les logs.

**Correction :** logger explicitement `RegexMatchTimeoutException` (warning + ID trigger + guild ID) et `ArgumentException` (pattern invalide → désactivation/suppression auto du trigger). Ne plus catcher `Exception` nu.

---

### 3. Pas de `RequireUserPermissions` sur `/feed create` et `/feed delete` ✅ Corrigé (2026-05-01, `ManageMessages` pour cohérence avec `/trigger`)
**Fichier :** `Commands/SlashCommands/Feeds.cs:103-167`

Les sous-commandes `create` et `delete` n'ont **aucun attribut de permission utilisateur**. À comparer avec `/trigger add` (`Triggers.cs:81`) qui exige `Permissions.ManageMessages` et `/clear` (`ClearMsgs.cs:19`) idem.

Conséquence : n'importe quel membre du guild peut créer jusqu'à `MaxFeedsPerGuild` feeds Reddit dans le channel courant, ou supprimer ceux des autres en connaissant le `feedId` (visible via `/feed list`). Vecteur d'abus : spam contrôlé via NSFW + un subreddit ciblé, ou suppression silencieuse des feeds légitimes du serveur.

**Correction :** ajouter `[RequireUserPermissions<ApplicationCommandContext>(Permissions.ManageChannels)]` (ou `ManageGuild`) sur `CreateNewFeed` et `DeleteFeed`. Dans `RemoveFeedAsync`, le scoping par `GuildId` est correct, mais la perm reste nécessaire en amont.

---

### 4. `AllowedMentions` jamais configuré → `@everyone`/`@role` exploitable ✅ Corrigé sur triggers + feeds (2026-05-01, `AllowedMentionsProperties.None`)
**Fichiers :** `Handlers/MessageCreateHandler.cs:34-36`, `Services/FeedService.cs:335-338`, `Commands/SlashCommands/Triggers.cs:50` (et globalement toutes les réponses).

Aucun envoi de message ne fixe `AllowedMentions = AllowedMentionsProperties.None` (ou un whitelist explicite). Vecteurs concrets :
- **Triggers** : un modo crée un trigger dont la `Response` contient `@everyone` → ping serveur entier déclenché par n'importe quel message correspondant.
- **Feeds Reddit** : un titre/permalink Reddit peut contenir `@everyone` ou `<@&roleId>` ; le bot relaie tel quel dans le channel.
- **WaifuApi / Reminder** : pas affecté pour l'instant (URLs uniquement) mais à durcir par défaut.

**Correction :** définir un `AllowedMentions` par défaut au niveau `MessageProperties` (None pour les feeds, et au minimum `Roles = false, Everyone = false` pour les triggers), partout où le contenu vient d'un utilisateur ou d'une source externe.

---

## 🟠 Important

### 5. `RegexOptions` accepté brut comme entier, pas de whitelist ✅ Corrigé (2026-05-01)
**Fichier :** `Commands/SlashCommands/Triggers.cs:93-97` → `MessageCreateHandler.cs:56`

```csharp
[SlashCommandParameter(Name = "regexoptions", ...)] int regexOptions = 0
...
Regex.IsMatch(msg.Content, pattern.Pattern, (RegexOptions)pattern.RegexOptions)
```

L'utilisateur passe un `int` arbitraire, casté en `RegexOptions`. Risques :
- `RegexOptions.Compiled` (`= 8`) compile un assembly dynamique par pattern, **non collecté** : memory leak progressif si abusé.
- Bits inconnus / combinaisons exotiques peuvent activer des comportements non testés.

**Correction :** masquer avec un whitelist (`IgnoreCase | Multiline | Singleline | IgnorePatternWhitespace` p.ex.) avant le cast, ou exposer une enum `[SlashCommandChoice]` au lieu d'un int libre.

---

### 6. Pattern jamais validé à la création ✅ Corrigé (2026-05-01)
**Fichier :** `Commands/SlashCommands/Triggers.cs:111` (`Triggers.Add`)

Le pattern est stocké en DB sans test de compilation préalable. Un pattern invalide (`[abc`, `(unclosed`) ne lève une `ArgumentException` qu'au runtime, message par message — exception aujourd'hui silenced par le catch du point 2.

**Correction :** dans `Add`, faire un `_ = new Regex(regex, allowedOptions, TimeSpan.FromMilliseconds(100));` dans un `try/catch` avant insert, et rejeter avec `SlashCommandBusinessException(InvalidRegexPattern)` en cas d'échec. Profiter de l'occasion pour faire un `Regex.IsMatch("test_string", regex, timeout)` qui détecte aussi un ReDoS naïf.

---

### 7. ID d'emote injecté dans l'URL CDN sans validation ✅ Corrigé (2026-05-01)
**Fichier :** `Commands/MessageCommands/DownloadEmotes.cs:83,94`

```csharp
new Regex("<(a)?:(.*?):(.*?)>", RegexOptions.ECMAScript);
...
string url = string.Format("https://cdn.discordapp.com/emojis/{0}.{1}?size=512&quality=lossless", id, ...);
```

Le 3ᵉ groupe `(.*?)` capture **n'importe quel caractère**, dont `?`, `&`, `/`, `#`. Aucun `Uri.EscapeDataString` ni vérif `ulong.TryParse(id, out _)`. Vecteur : un message contenant `<:x:123?attack=1>` produit une URL malformée, voire un appel à un endpoint Discord inattendu (limité, mais pas zéro).

**Correction :** durcir la regex à `<(a)?:([\w]{2,32}):(\d{17,20})>` (ID Discord = snowflake numérique 17–20 chiffres, nom emote alphanumérique), et faire un `ulong.TryParse(id, out _)` en garde-fou avant ajout à la liste.

---

### 8. Pas de borne sur la taille des téléchargements emotes ✅ Corrigé (2026-05-01)
**Fichier :** `Commands/MessageCommands/DownloadEmotes.cs:112` + `Program.cs:62-64` (HttpClient `AnimeImages`)

`client.GetByteArrayAsync(e.Url)` lit la totalité en mémoire, en parallèle (`Task.WhenAll` sur jusqu'à 20 emotes). Pas de `MaxResponseContentBufferSize` configuré sur les HttpClients nommés, ni de check `Content-Length` avant lecture. Si le CDN renvoie un fichier anormal (gros gif), le process gonfle en mémoire.

**Correction :** sur l'`AddHttpClient` correspondant, fixer `client.MaxResponseContentBufferSize = 8 * 1024 * 1024;` (Discord cap les emojis à 256 KB, on prend large), ou streamer directement vers le ZipArchive au lieu de tout buffer.

---

### 9. Secrets (token bot, credentials Postgres) en clair sur disque
**Fichier :** `Program.cs:14-16` + `appsettings.json`

Le `ConfigurationBuilder` ne charge **que** `appsettings.json` :
```csharp
new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
```

Pas de `AddEnvironmentVariables()`, pas de `AddUserSecrets()`, pas de `appsettings.{Environment}.json`. Le `.gitignore` protège bien le fichier du repo, mais en prod le token et le mot de passe Postgres restent en clair sur le filesystem, sans rotation ni override par variable d'env.

**Correction :** chaîner `.AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables(prefix: "VEGA_").AddUserSecrets<Program>(optional: true)` ; documenter dans le README l'override via env (ex. `VEGA_botToken`, `VEGA_postgres__connexionString`).

---

## 🟡 Mineur

### 10. Pas de log d'audit pour `/clear`
**Fichier :** `Commands/SlashCommands/ClearMsgs.cs:38-65`

La commande supprime jusqu'à 100 messages mais n'enregistre rien (ni en DB, ni vers un channel d'audit Discord). Un modo malveillant peut nettoyer les preuves d'une conversation sans laisser de trace côté bot. Discord garde un audit log natif limité, mais sans le contenu.

**Correction :** logger l'action via `Serilog` (UserId, ChannelId, GuildId, count) et exposer un channel de logs configurable par guild (extension de `GuildSettings`).

---

### 11. Pattern et response des triggers loggés en clair
**Fichier :** `Handlers/MessageCreateHandler.cs:40` (et plus largement, le pattern stocké en DB)

```csharp
Log.Error(ex, "Failed to send response to trigger {0} in server {1}", trigger?.TriggerId, message.GuildId);
```

OK ici (seul le TriggerId est loggé), mais ailleurs (`FeedService.cs:88-89`, `FeedService.cs:340`) on logge librement `feed.Topic`, ce qui est OK car publique. Vérifier qu'aucun log ne sort `pattern.Pattern` ni `pattern.Response` brut (contenu utilisateur, peut contenir des données sensibles ou des injections de log si `\n` dans pattern).

**Correction :** ne jamais logger un `Pattern`/`Response` sans le sérialiser entre quotes, et préférer logger uniquement le `TriggerId`. Ajouter un sanitize si on doit absolument logger le contenu.

---

### 12. Dépendances : pas de scan documenté
**Fichier :** projet entier (`VEGA.csproj`, `VEGA.Tests.csproj`)

Pas de `dotnet list package --vulnerable` dans la CI, pas de Dependabot/Renovate visible. Polly, Npgsql, Netcord, Serilog évoluent rapidement.

**Correction :** ajouter une étape CI `dotnet list package --vulnerable --include-transitive` qui fail le build sur une CVE, ou activer Dependabot.

---

### 13. Pas de transaction sur `SaveOrUpdateAsync`
**Fichier :** `Services/GuildSettingsService.cs:73-124`

Le diff triggers (Add/Modify/Delete) puis `SaveChanges` se fait en plusieurs requêtes sans `BeginTransaction`. Deux modos modifiant en même temps les triggers d'un même guild peuvent voir une partie des changements perdue (last-write-wins, voire incohérences).

**Correction :** wrapper l'ensemble dans `await using var tx = _dbContext.Database.BeginTransactionAsync()` + `tx.CommitAsync()`. Pas un risque sécu majeur, mais intégrité de données.

---

### 14. `FeedConfiguration` modifiable sans audit
**Fichier :** `Commands/SlashCommands/Backoffice/FeedConfig.cs:53-126`

`/feedconfig set` est protégé par `[RequireSuperAdmin]` (✓), mais aucune trace n'est conservée de qui change quoi (ex. passage de `MaxFeedsPerGuild` de 5 à 10000). Pour une conf globale partagée, un log d'audit minimal serait sain.

**Correction :** `_logger.LogInformation("SuperAdmin {UserId} changed {Param} from {Old} to {New}", ...)` avant `UpdateConfigAsync`.

---

## 📋 Priorités recommandées

| Priorité | Action | Fichier(s) |
|----------|--------|-----------|
| 🔴 Immédiat | Timeout regex + log des erreurs trigger | `MessageCreateHandler.cs` |
| 🔴 Immédiat | `RequireUserPermissions` sur `/feed create` et `/feed delete` | `Feeds.cs` |
| 🔴 Immédiat | `AllowedMentions = None` sur triggers et feeds | `MessageCreateHandler.cs`, `FeedService.cs` |
| 🟠 Court terme | Whitelist `RegexOptions` + validation pattern à la création | `Triggers.cs` |
| 🟠 Court terme | Validation stricte ID emote + `MaxResponseContentBufferSize` | `DownloadEmotes.cs`, `Program.cs` |
| 🟠 Court terme | `AddEnvironmentVariables` pour secrets | `Program.cs` |
| 🟡 Moyen terme | Audit log `/clear` et `/feedconfig set` | `ClearMsgs.cs`, `FeedConfig.cs` |
| 🟡 Moyen terme | Transaction sur `SaveOrUpdateAsync` | `GuildSettingsService.cs` |
| 🟡 Moyen terme | Scan dépendances en CI | CI / `.github` |
