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
    // TODO: Refactor test methods to remove most of the shared code (initialization, message verification)
    [TestClass]
    public class AdminBotCommandHandlerTests
    {
        const ulong DefaultUserId = 1234;
        const ulong GuildId = 123456;
        const string GuildName = "GuildName";
        const string TournamentName = "New Tournament";

        [TestMethod]
        public async Task AddTournamentDirector()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser guildUser = CreateGuildUser(DefaultUserId);
            await commandHandler.AddTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(
                BotStrings.AddTournamentDirectorSuccessful, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            Assert.IsTrue(
                manager.TryGetTournament(TournamentName, out ITournamentState state), "Could not find tournament.");
            Assert.IsTrue(state.IsDirector(DefaultUserId), "Director was not added.");
        }

        [TestMethod]
        public async Task AddTwoTournamentDirectors()
        {
            const ulong firstUserId = 123;
            const ulong secondUserId = 1234;
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            AddTournamentDirectorDirectly(globalManager, firstUserId);

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser guildUser = CreateGuildUser(secondUserId);
            await commandHandler.AddTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(
                BotStrings.AddTournamentDirectorSuccessful, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            Assert.IsTrue(
                manager.TryGetTournament(TournamentName, out ITournamentState state), "Could not find tournament.");
            Assert.IsTrue(state.IsDirector(firstUserId), "First director was not added.");
            Assert.IsTrue(state.IsDirector(secondUserId), "Second director was not added.");
        }

        [TestMethod]
        public async Task AddSameTournamentDirectors()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            AddTournamentDirectorDirectly(globalManager, DefaultUserId);

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser guildUser = CreateGuildUser(DefaultUserId);
            await commandHandler.AddTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(
                BotStrings.UserAlreadyTournamentDirector, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            Assert.IsTrue(
                manager.TryGetTournament(TournamentName, out ITournamentState state), "Could not find tournament.");
            Assert.IsTrue(state.IsDirector(DefaultUserId), "User should still be a director.");
        }

        [TestMethod]
        public async Task RemoveTournamentDirector()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            AddTournamentDirectorDirectly(globalManager, DefaultUserId);

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser guildUser = CreateGuildUser(DefaultUserId);
            await commandHandler.RemoveTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(BotStrings.RemovedTournamentDirector, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            Assert.IsTrue(
                manager.TryGetTournament(TournamentName, out ITournamentState state), "Could not find tournament.");
            Assert.IsFalse(state.IsDirector(DefaultUserId), "Director was not removed.");
        }

        [TestMethod]
        public async Task RemoveNonexistentTournamentDirector()
        {
            const ulong otherId = DefaultUserId + 1;
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            AddTournamentDirectorDirectly(globalManager, DefaultUserId);

            IGuildUser guildUser = CreateGuildUser(otherId);
            await commandHandler.RemoveTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(BotStrings.UserNotTournamentDirector, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            Assert.IsTrue(
                manager.TryGetTournament(TournamentName, out ITournamentState state), "Could not find tournament.");
            Assert.IsFalse(state.IsDirector(otherId), "Director should not have been added.");
        }

        [TestMethod]
        public async Task RemoveFromNonexistentTournament()
        {
            const ulong otherId = DefaultUserId + 1;
            MessageStore messageStore = new MessageStore();
            ICommandContext context = MockICommandContextFactory.CreateCommandContext(GuildId, GuildName, messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser guildUser = CreateGuildUser(otherId);
            await commandHandler.RemoveTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(BotStrings.TournamentDoesNotExist, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);
        }

        // TODO: Add test for ClearAll that checks that all artifacts are cleared.

        private static void AddTournamentDirectorDirectly(GlobalTournamentsManager globalManager, ulong userId)
        {
            TournamentsManager manager = globalManager.GetOrAdd(
                GuildId,
                id => new TournamentsManager()
                {
                    GuildId = id
                });
            ITournamentState state = manager.AddOrUpdateTournament(
                TournamentName,
                new TournamentState(GuildId, TournamentName),
                (name, oldState) => oldState);
            Assert.IsTrue(state.TryAddDirector(userId), "First TD added should occur.");
        }

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
