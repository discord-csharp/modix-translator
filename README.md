# modix-translator - A translation bot for discord

modix-translator aims to help break down the language barrier experienced when conversing with server memberts from all over the world. modix-translator leverages Azure Cognative Services translator api in order to provide a more natural translation. See https://azure.microsoft.com/en-us/services/cognitive-services/translator-text-api/ for more details

## Getting Started

Below is a guide on how to start developing modix-translator. Development is straight forward requiring only the latest preview of .net 5

# Prerequisites
To work on modix-translator, you need a few things:
- A Discord application set up - [go here to create one](https://discordapp.com/developers/applications/), add a bot to it, and copy the **token** from the page. You can then add the bot to your server by going to ` https://discordapp.com/oauth2/authorize?scope=bot&permissions=1342565456&client_id=[ID HERE]`, replacing `[ID HERE]` with the **Client ID** of your bot (not the token).
- [The latest .NET 5 SDK for your chosen platform](https://dotnet.microsoft.com/download/dotnet/5.0?utm_source=dotnet-website&utm_medium=banner&utm_campaign=preview5-banner) (currently 5)
- **Optional**: [Docker](https://www.docker.com/get-docker). You **do not** need Docker if you're just developing locally - it's mostly just to test if your changes are significant enough that they might break CI, or if you prefer to keep your dev environment clean. If you're on Windows, make sure you switch to Linux containers.

# Setting Configuration
### Config file
This project leverages UserSecrets to provide a bot token and api key to the project. Additionally, the hosting environment can be set with an environment variable.

### Environment Variables
If you prefer to use environment variables for configuration, they must all be prefixed with **`BOT_`**. For example, **`BOT_DiscordToken`**, **`BOT_AzureTranslationKey`**, etc.

### List of config options
- **Required**
  - `AzureTranslationKey` - This is the key for the azure translator service.
  - `DiscordToken` - this is the bot token modix-translator should use to connect to the Discord API. See above.

### Azure Translator Service
This service can be used with the free tier of the Azure translator in the `global` region.

## Hosting

Due to the costs associated with high volume translation, we cannot offer this as a hosted service. You will need to create your own Azure translation service account and self-host this bot.

