## Quiz Bowl Discord Tournament Assistant

### Introduction

This bot creates the channels and roles for a round robin quiz bowl tournament on Discord. It ensures that teams can only see text channels for games they will play in. Tournaments can be set up in minutes, saving tournament directors time in manually creating rooms and roles.


### Instructions

#### Running the bot

There are two options for running the bot on your sever: hosting it locally on your machine, or contacting the author and asking them for an invitation link to add his bot to your server.

If you decide to host it locally, then do the following:

1. Install [.Net Core Runtime 3.1](https://dotnet.microsoft.com/download/dotnet-core/3.1)  
2. Unzip the release
3. Go to https://discordapp.com/developers to register your instance of the bot
    - Update discordToken.txt with the client secret from your registered Discord bot
    - Visit this site (with your bot's client ID) to add the bot to your channel
      - https://discordapp.com/oauth2/authorize?client_id=CLIENTID&scope=bot
4. Run the .exe file
5. Grant your bot the permissions in the Bot Permissions section.

If you want to use the author's version of the bot, then do the following:

1. Ask the author for permission to use the bot for your tournament.
2. The author will send you a link to open (of the form https://discordapp.com/oauth2/authorize?client_id=CLIENTID&scope=bot), which will open a web page asking you if you agree to add the bot to your server. You must be the server administrator.
3. Grant your bot the permissions in the Bot Permissions section.

#### Bot Permissions

The bot needs to have all of these permissions (give it a role), because it needs to grant readers these permissions:

- Use Voice Activity
- Manage Roles
- Kick Members
- Read Text Channels & See Voice Channels
- Send Messages
- Read Message History
- Add Reactions
- Connect
- Speak
- Mute Members
- Defean Members
- Move Members
- Priority Speaker

#### Running a tournament

  1. A server admin creates your tournament by adding yourself as a TD to a tournament.
  
    !addTD @my_user <My tournament name>

  2. The tournament director starts the setup phase with !setup. The bot will then ask the tournament director some questions (steps 3-5).

    !setup

  3. Add all of the readers based on their mentions.

    @Reader1  @Reader2

  4. Set the number of round robins to play with

    3

  5. Add all of the teams in the tournament

    Team 1, Team 2, Team 3, Team 4

  6. The bot will send out a message or two telling players which reaction to click to join their team.

  7. Once every player has joined a team, the tournament director then start the tournament with !start.

    !start

  8. The bot will create the text and voice channels for everyone, and assign permissions for these rooms. It will tell you when this is completed.
  
  9. If you need to rebracket, then the tournament director should use the !rebracket command. When the bot asks you for the teams, give them in the same way that they were initially, with teams in the same bracket belonging to the same line.
  
    !rebracket

    <after the bot asks you for the brackets>
     
    Team 1, Team 4
    Team 2, Team 3
  
  10. If you need to set up a finals room, then the tournament director should use the !finals command.

    !finals @Reader Team1, Team2

  11. To end the tournament, the tournament director uses !end

    !end

Note: if your tournament has different brackets, then put each team in the same bracket on the same line during step 5. For example, if your tournament had two brackets, and teams X and Y were in one bracket and teams Alpha and Omega were in the other, send this:

    X, Y
    Alpha, Omega

Note: you can find all of the commands that the bot supports by direct messaging it this command

    !help

### Development

To build the bot, download the .Net Core SDK (at least version 3.1), and then run

    dotnet build

#### Requirements

- [.Net Core 3.1 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.1)
  - If using Visual Studio, you need Visual Studio 2017.5
- Libraries from Nuget:
  - Discord.Net
  - Moq
  - Serilog
  - Microsoft.CodeAnalysis.FxCopAnalyzers
  - MSTest libraries (MSTest.TestAdapter, MSTest.TestFramework, Microsoft.Net.Test.SDK)
  - These may be automatically downloaded. If not, you can get them by Managing your Nuget references in the Visual Studio solution.
- You will need to create your own Discord bot at https://discordapp.com/developers. Follow the steps around creating your bot in Discord mentioned in the "Running the bot on your own machine" section.