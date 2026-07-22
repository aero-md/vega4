# VEGA Bot
A multi purpose bot using DotNet9 and [Netcord](https://netcord.dev/)

## List of available commands
1. Slash commands
    - Trigger add/list/delete : Allow create/read/delete of "triggers". These are made of a patterns (regex), response and other options. If the pattern is detected in a message, the bot will send the response in a text message replying to the message matching the pattern. Patterns are guild (Discord server) scoped
    - Clear messages : used to quickly clear between 1 and 50 messages at once in a text channel
    - Show profile : shows the profile picture, tag, banner and other public informations of a users Discord account
    - Up : displays information about the bot as well as its uptime 
    - Waifu : posts a random anime image fetched from public APIs
    - Diceroll : toss between 1 and 100 dices, with between 2 and 100 faces
    - (Debug only) ClearCommands : clears all registered commands for the bot. Used in development process to test commands registration
2. Message commands
    - Download emotes : download all emotes in the targeted message as high res png/gif and send them in a single zip file
3. User commands
    - ID : returns the targeted user's Discord ID in an ephemeral message

All commands require that users using them have the required permissions to do what the command does. Example : clear messages requires the user to have ManageMessages permission. Download emotes requires the EmbedFile permission.

## How to use

Requirements : a PostgreSQL Database. *DB scripts or DB project are still to be added to the repo*

Download source, copy `appsettings-exemple.json` to `appsettings.json`, and fill it with your Discord bot token and PostgreSQL connection string