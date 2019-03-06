﻿using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;
using System.Threading.Tasks;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class GuildBotCommandHandlerTests : CommandHandlerTestBase
    {
        const ulong DefaultUserId = 2;

        [TestMethod]
        public async Task NoCurrentTournament()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            await commandHandler.GetCurrentTournament();
            // TODO: Move to resx file
            string expectedMessage = string.Format(
                BotStrings.UnableToPerformCommand, "No current tournament is running.");
            messageStore.VerifyDirectMessages(expectedMessage);
        }

        [TestMethod]
        public async Task GetCurrentTournament()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            this.AddCurrentTournament(globalManager);

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            await commandHandler.GetCurrentTournament();
            string expectedMessage = string.Format(
                BotStrings.CurrentTournamentInGuild, DefaultGuildName, DefaultTournamentName);
            messageStore.VerifyDirectMessages(expectedMessage);
        }
    }
}
