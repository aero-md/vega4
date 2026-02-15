namespace Resources;

public static class Strings
{
    public static class Misc
    {
        public const string Unknown = "misc.unknown";
        public const string PlaceHolder = "misc.placeholder";
    }
    public static class Exceptions
    {
        public const string MissingPermission = "exceptions.missingPermission";
        public const string RequireSuperAdmin = "exceptions.requireSuperAdmin";
        public const string IncorrectId = "exceptions.incorrectId";
        public const string UserNotFound = "exceptions.userNotFound";
        public const string UnableToRetrieveGuild = "exceptions.unableToRetrieveGuild";
        public const string InvalidParams = "exceptions.invalidParams";
        public const string BotMissingPermissions = "exceptions.botMissingPermissions";
        public const string UserMissingPermissions = "exceptions.userMissingPermissions";
        public const string CommandExecutionFailed = "exceptions.commandExecutionFailed";
        public const string CommandExecutionCritical = "exceptions.commandExecutionCritical";
        public const string TriggerNotFound = "exceptions.triggerNotFound";
        public const string WaifuApiCallFailed = "exceptions.waifuApiCallFailed";
        public const string NoEmoteInMessage = "exceptions.noEmoteInMessage";
        public const string TooManyEmotesInMessage = "exceptions.tooManyEmotesInMessage";
    }

    public static class Commands
    {
        public const string Username = "commands.username";
        public const string CreationDate = "commands.creationDate";
        public const string UserId = "commands.userId";
        public const string DeletedMessages = "commands.deletedMessages";
        public const string ClearedCommandsGlobal = "commands.clearedCommandsGlobal";
        public const string ClearedCommandsGuild = "commands.clearedCommandsGuild";
        public const string UserIdResponse = "commands.userIdResponse";
        public const string NoTriggersOnServer = "commands.noTriggersOnServer";
        public const string CurrentTriggersHeader = "commands.currentTriggersHeader";
        public const string TriggerInfo = "commands.triggerInfo";
        public const string TriggerAdded = "commands.triggerAdded";
        public const string TriggerDeleted = "commands.triggerDeleted";
        public const string NoActiveFeedsOnServer = "commands.noActiveFeedsOnServer";
        public const string FeedCreated = "commands.feedCreated";
        public const string ActiveFeedsOnServer = "commands.activeFeedsOnServer";
        public const string FeedDelay = "commands.feedDelay";
        public const string DlEmotesResultSingle = "commands.dlEmotesResultSingle";
        public const string DlEmotesResultMultiple = "commands.dlEmotesResultMultiple";
        public const string ReminderTimeRequired = "commands.reminderTimeRequired";
        public const string ReminderInvalidTime = "commands.reminderInvalidTime";
        public const string ReminderSet = "commands.reminderSet";
        public const string NoActiveReminders = "commands.noActiveReminders";
        public const string YourActiveReminders = "commands.yourActiveReminders";
        public const string ReminderDeleted = "commands.reminderDeleted";
        public const string RemindersReset = "commands.remindersReset";
        public const string ReminderSnoozed = "commands.reminderSnoozed";
        public const string InvalidGuildIdFormat = "commands.invalidGuildIdFormat";
        public const string CacheClearedForGuild = "commands.cacheClearedForGuild";
        public const string CacheNotFoundForGuild = "commands.cacheNotFoundForGuild";
        public const string ErrorClearingCache = "commands.errorClearingCache";
        public const string CacheClearedForCurrentGuild = "commands.cacheClearedForCurrentGuild";
        public const string CacheNotFoundForCurrentGuild = "commands.cacheNotFoundForCurrentGuild";
        public const string CacheInfoTitle = "commands.cacheInfoTitle";
        public const string CacheInfoDescription = "commands.cacheInfoDescription";
        public const string CacheInfoDurationLabel = "commands.cacheInfoDurationLabel";
        public const string CacheInfoDurationValue = "commands.cacheInfoDurationValue";
        public const string CacheInfoDataLabel = "commands.cacheInfoDataLabel";
        public const string CacheInfoDataValue = "commands.cacheInfoDataValue";
        public const string CacheInfoFooter = "commands.cacheInfoFooter";
    }
    
    public static class Logs
    {
        public const string InteractionTimeoutWarning = "logs.interactionTimeoutWarning";
        public const string FailedToSendInteractionResponse = "logs.failedToSendInteractionResponse";
    }
}