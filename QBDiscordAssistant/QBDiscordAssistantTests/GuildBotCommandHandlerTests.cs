using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class GuildBotCommandHandlerTests : CommandHandlerTestBase
    {
        private const ulong DefaultUserId = 2;

        [TestMethod]
        public async Task NoCurrentTournament()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            await commandHandler.GetCurrentTournamentAsync();
            string expectedMessage = BotStrings.UnableToPerformCommand(TournamentStrings.NoCurrentTournamentRunning);
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
            await commandHandler.GetCurrentTournamentAsync();
            string expectedMessage = BotStrings.CurrentTournamentInGuild(DefaultGuildName, DefaultTournamentName);
            messageStore.VerifyDirectMessages(expectedMessage);
        }
    }
}
