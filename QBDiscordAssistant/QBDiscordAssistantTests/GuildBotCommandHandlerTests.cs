using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;
using System.Threading.Tasks;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class GuildBotCommandHandlerTests
    {
        const ulong DefaultUserId = 2;
        const ulong GuildId = 1;
        const string GuildName = "TournamentGuild";
        const string TournamentName = "New Tournament";

        [TestMethod]
        public async Task NoCurrentTournament()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            await commandHandler.GetCurrentTournament();
            // TODO: Move to resx file
            string expectedMessage = string.Format(
                BotStrings.UnableToPerformCommand, "No current tournament is running.");
            messageStore.VerifyMessages(expectedMessage);
        }

        [TestMethod]
        public async Task GetCurrentTournament()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            ITournamentState state = new TournamentState(GuildId, TournamentName);
            state = manager.AddOrUpdateTournament(TournamentName, state, (name, oldState) => state);
            Assert.IsTrue(
                manager.TrySetCurrentTournament(TournamentName, out string errorMessage), 
                "We should be able to set the current tournament.");

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            await commandHandler.GetCurrentTournament();
            string expectedMessage = string.Format(
                BotStrings.CurrentTournamentInGuild, GuildName, TournamentName);
            messageStore.VerifyMessages(expectedMessage);
        }

        // TODO: potentially move this and CreateCommandContext to protected methods in a test base class
        private static IGuildUser CreateGuildUser(ulong userId)
        {
            Mock<IGuildUser> mockGuildUser = new Mock<IGuildUser>();
            mockGuildUser
                .Setup(user => user.Id)
                .Returns(userId);
            return mockGuildUser.Object;
        }
    }
}
