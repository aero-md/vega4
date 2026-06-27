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
        public const string InvalidRegexPattern = "exceptions.invalidRegexPattern";
        public const string WaifuApiCallFailed = "exceptions.waifuApiCallFailed";
        public const string InvalidCategorySelected = "exceptions.invalidCategorySelected";
        public const string UnimplementedApi = "exceptions.unimplementedApi";
        public const string ReminderNotFound = "exceptions.reminderNotFound";
        public const string GuildIdMismatch = "exceptions.guildIdMismatch";
        public const string FeedLimitReached = "exceptions.feedLimitReached";
        public const string FeedNotFound = "exceptions.feedNotFound";
        public const string InvalidFeedIdFormat = "exceptions.invalidFeedIdFormat";
        public const string FeedConfigInvalidValue = "exceptions.feedConfigInvalidValue";
        public const string NoEmoteInMessage = "exceptions.noEmoteInMessage";
        public const string TooManyEmotesInMessage = "exceptions.tooManyEmotesInMessage";
        public const string DlEmotesWidgetExpired = "exceptions.dlEmotesWidgetExpired";
        public const string DlEmotesNotInGuild = "exceptions.dlEmotesNotInGuild";
        public const string DlEmotesBotMissingEmojiPerm = "exceptions.dlEmotesBotMissingEmojiPerm";
        public const string DlEmotesUserMissingEmojiPerm = "exceptions.dlEmotesUserMissingEmojiPerm";
        public const string DlEmotesNotInvoker = "exceptions.dlEmotesNotInvoker";
        public const string DlEmotesAddFailed = "exceptions.dlEmotesAddFailed";
        public const string PollNotFoundOrEnded = "exceptions.pollNotFoundOrEnded";
        public const string TriggerManageRequired = "exceptions.triggerManageRequired";
        public const string FeedManageRequired = "exceptions.feedManageRequired";
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
        public const string TriggerBtnDelete = "commands.triggerBtnDelete";
        public const string TriggerDeleteMenuPrompt = "commands.triggerDeleteMenuPrompt";
        public const string TriggerDeleteMenuPlaceholder = "commands.triggerDeleteMenuPlaceholder";
        public const string TriggerDeleteConfirmPrompt = "commands.triggerDeleteConfirmPrompt";
        public const string TriggerBtnConfirm = "commands.triggerBtnConfirm";
        public const string TriggerBtnCancel = "commands.triggerBtnCancel";
        public const string TriggerDeleteCancelled = "commands.triggerDeleteCancelled";
        public const string TriggerBtnAdd = "commands.triggerBtnAdd";
        public const string TriggerAddModalTitle = "commands.triggerAddModalTitle";
        public const string TriggerAddFieldRegex = "commands.triggerAddFieldRegex";
        public const string TriggerAddFieldResponse = "commands.triggerAddFieldResponse";
        public const string TriggerAddFieldOptions = "commands.triggerAddFieldOptions";
        public const string NoActiveFeedsOnServer = "commands.noActiveFeedsOnServer";
        public const string FeedCreated = "commands.feedCreated";
        public const string ActiveFeedsOnServer = "commands.activeFeedsOnServer";
        public const string FeedDelay = "commands.feedDelay";
        public const string FeedDeleted = "commands.feedDeleted";
        public const string FeedStatusActive = "commands.feedStatusActive";
        public const string FeedStatusChannelDeleted = "commands.feedStatusChannelDeleted";
        public const string FeedStatusTopicUnavailable = "commands.feedStatusTopicUnavailable";
        public const string FeedStatusSuspended = "commands.feedStatusSuspended";
        public const string FeedBtnAdd = "commands.feedBtnAdd";
        public const string FeedBtnDelete = "commands.feedBtnDelete";
        public const string FeedAddModalTitle = "commands.feedAddModalTitle";
        public const string FeedAddFieldTopic = "commands.feedAddFieldTopic";
        public const string FeedAddFieldInterval = "commands.feedAddFieldInterval";
        public const string FeedAddFieldStartAt = "commands.feedAddFieldStartAt";
        public const string FeedAddFieldNsfw = "commands.feedAddFieldNsfw";
        public const string FeedDeleteMenuPrompt = "commands.feedDeleteMenuPrompt";
        public const string FeedDeleteMenuPlaceholder = "commands.feedDeleteMenuPlaceholder";
        public const string FeedDeleteConfirmPrompt = "commands.feedDeleteConfirmPrompt";
        public const string FeedConfigUpdated = "commands.feedConfigUpdated";
        public const string FeedConfigModalTitle = "commands.feedConfigModalTitle";
        public const string FeedConfigSaved = "commands.feedConfigSaved";
        public const string DlEmotesResultSingle = "commands.dlEmotesResultSingle";
        public const string DlEmotesResultMultiple = "commands.dlEmotesResultMultiple";
        public const string DlEmotesWidgetSingle = "commands.dlEmotesWidgetSingle";
        public const string DlEmotesWidgetMultiple = "commands.dlEmotesWidgetMultiple";
        public const string DlEmotesBtnZip = "commands.dlEmotesBtnZip";
        public const string DlEmotesBtnAdd = "commands.dlEmotesBtnAdd";
        public const string DlEmotesAddedAll = "commands.dlEmotesAddedAll";
        public const string DlEmotesAddedPartial = "commands.dlEmotesAddedPartial";
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
        public const string PollTitle = "commands.pollTitle";
        public const string PollModalTitle = "commands.pollModalTitle";
        public const string PollFieldQuestion = "commands.pollFieldQuestion";
        public const string PollFieldDuration = "commands.pollFieldDuration";
        public const string PollFieldOptions = "commands.pollFieldOptions";
        public const string PollFieldImage = "commands.pollFieldImage";
        public const string PollEndsAt = "commands.pollEndsAt";
        public const string PollOptionYes = "commands.pollOptionYes";
        public const string PollOptionNo = "commands.pollOptionNo";
        public const string PollVoteRegistered = "commands.pollVoteRegistered";
        public const string PollVoteAlreadyVoted = "commands.pollVoteAlreadyVoted";
        public const string PollResultsTitle = "commands.pollResultsTitle";
        public const string PollResultsField = "commands.pollResultsField";
        public const string PollResultsFooter = "commands.pollResultsFooter";
    }
    
    public static class Logs
    {
        public const string InteractionTimeoutWarning = "logs.interactionTimeoutWarning";
        public const string FailedToSendInteractionResponse = "logs.failedToSendInteractionResponse";
    }
}