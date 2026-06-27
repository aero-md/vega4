# VEGA End-to-End Tests

## Configuration

Les tests end-to-end nécessitent une configuration appropriée pour se connecter à Discord.

### Étape 1 : Créer un bot de test

1. Créez un bot Discord dédié aux tests sur le [Discord Developer Portal](https://discord.com/developers/applications)
2. Copiez le token du bot
3. Invitez le bot sur un serveur de test avec les permissions nécessaires

### Étape 2 : Configurer appsettings.test.json

Modifiez le fichier `VEGA.Tests/appsettings.test.json` avec vos valeurs :

```json
{
  "botToken": "VOTRE_TOKEN_BOT_TEST",
  "postgres": {
    "connexionString": "Host=localhost;Database=vega_test;Username=postgres;Password=postgres"
  },
  "testGuildId": "ID_SERVEUR_TEST",
  "testChannelId": "ID_CANAL_TEST",
  "superAdminUserIds": []
}
```

**⚠️ Important** : Ne jamais commit votre token réel dans le dépôt !

### Étape 3 : Obtenir les IDs

- **Guild ID** : Activez le mode développeur Discord → Clic droit sur votre serveur → Copier l'ID du serveur
- **Channel ID** : Clic droit sur un canal de test → Copier l'ID du salon

## Exécution des tests

### Tous les tests
```powershell
cd VEGA.Tests
dotnet test
```

### Tests end-to-end uniquement
```powershell
cd VEGA.Tests
dotnet test --filter "FullyQualifiedName~VegaEndToEndTests"
```

### Un test spécifique
```powershell
dotnet test --filter "FullyQualifiedName~VegaEndToEndTests.BotShouldBeConnectedAndReachable"
```

## Tests disponibles

### Tests de connexion
- `BotShouldBeConnectedAndReachable` - Vérifie que le bot est connecté
- `BotShouldBeInTestGuild` - Vérifie que le bot est dans le serveur de test

### Tests de commandes
- `UpCommand_ShouldHaveRegisteredCommandInGuild` - Vérifie que /up est enregistrée
- `ClearCommand_ShouldHaveRegisteredCommandInGuild` - Vérifie que /clear est enregistrée

### Tests de fonctionnalités
- `Clear_ShouldDeleteMessagesWhenInvoked` - Teste la suppression de messages
- `Clear_ShouldHandleBulkDeletion` - Teste la suppression en masse
- `BotCanSendAndReceiveMessages` - Teste l'envoi/réception de messages
- `BotCanSendEmbed` - Teste l'envoi d'embeds

## Architecture

Les tests utilisent une fixture (`VegaBotFixture`) qui :
- Crée une **seule instance** du bot au début de tous les tests
- Fournit un `RestClient` pour vérifier les résultats
- Nettoie automatiquement à la fin

```csharp
[Collection("VegaBotCollection")]
public class VegaEndToEndTests : IClassFixture<VegaBotFixture>
{
    // Tous les tests partagent la même instance du bot
}
```

## Notes importantes

1. **Instance unique** : Le bot est créé une seule fois pour tous les tests
2. **Tests asynchrones** : Tous les tests utilisent `async/await`
3. **Nettoyage** : Les messages de test sont supprimés après chaque test
4. **Rate limits** : Des délais sont ajoutés pour éviter les limites de Discord

## Dépannage

### Le bot ne se connecte pas
- Vérifiez que le token est correct
- Vérifiez que le bot a les intents nécessaires activés

### Tests qui échouent
- Assurez-vous que le bot a les permissions nécessaires dans le canal de test
- Vérifiez les IDs de serveur et canal
- Attendez quelques secondes entre les exécutions de tests

### Erreurs de base de données
- Créez une base de données PostgreSQL de test
- Vérifiez la chaîne de connexion dans appsettings.test.json
