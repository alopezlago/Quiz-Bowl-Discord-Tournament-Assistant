using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class AdminBotCommandHandlerTests
    {
        const ulong GuildId = 123456;
        const string GuildName = "GuildName";
        const string TournamentName = "New Tournament";

        [TestMethod]
        public async Task AddTournamentDirector()
        {
            const ulong userId = 1234;
            MessageStore messageStore = new MessageStore();
            ICommandContext context = CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser guildUser = CreateGuildUser(userId);
            await commandHandler.AddTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(
                BotStrings.AddTournamentDirectorSuccessful, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            Assert.IsTrue(
                manager.TryGetTournament(TournamentName, out ITournamentState state), "Could not find tournament.");
            Assert.IsTrue(state.IsDirector(userId), "Director was not added.");
        }

        [TestMethod]
        public async Task AddTwoTournamentDirectors()
        {
            const ulong firstUserId = 123;
            const ulong secondUserId = 1234;
            MessageStore messageStore = new MessageStore();
            ICommandContext context = CreateCommandContext(messageStore);
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
        public async Task RemoveTournamentDirector()
        {
            const ulong userId = 1234;
            MessageStore messageStore = new MessageStore();
            ICommandContext context = CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            AddTournamentDirectorDirectly(globalManager, userId);

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser guildUser = CreateGuildUser(userId);
            await commandHandler.RemoveTournamentDirector(guildUser, TournamentName);
            string expectedMessage = string.Format(BotStrings.RemovedTournamentDirector, TournamentName, GuildName);
            messageStore.VerifyMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(GuildId, id => new TournamentsManager());
            Assert.IsTrue(
                manager.TryGetTournament(TournamentName, out ITournamentState state), "Could not find tournament.");
            Assert.IsFalse(state.IsDirector(userId), "Director was not removed.");
        }

        // TODO: Add test for the case where the tournament doesn't exist. May want a message then.
        // TODO: Add resource file to hold message strings. Then compare messages sent to resource file.

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

        private static ICommandContext CreateCommandContext(MessageStore messageStore)
        {
            Mock<IUserMessage> mockUserMessage = new Mock<IUserMessage>();

            Mock<IDMChannel> mockDmChannel = new Mock<IDMChannel>();
            mockDmChannel
                .Setup(dmChannel => dmChannel.SendMessageAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), null))
                .Returns<string, bool, Embed, RequestOptions>((message, isTTS, embed, options) =>
                {
                    messageStore.DirectMessages.Add(message);
                    return Task.FromResult(mockUserMessage.Object);
                });

            Mock<IUser> mockUser = new Mock<IUser>();
            mockUser
                .Setup(user => user.GetOrCreateDMChannelAsync(null))
                .Returns(Task.FromResult(mockDmChannel.Object));

            Mock<ICommandContext> mockContext = new Mock<ICommandContext>();

            Mock<IGuild> mockGuild = new Mock<IGuild>();
            mockGuild
                .Setup(guild => guild.Id)
                .Returns(GuildId);
            mockGuild
                .Setup(guild => guild.Name)
                .Returns(GuildName);
            mockContext
                .Setup(context => context.Guild)
                .Returns(mockGuild.Object);
            mockContext
                .Setup(context => context.User)
                .Returns(mockUser.Object);

            return mockContext.Object;
        }

        private static IGuildUser CreateGuildUser(ulong userId)
        {
            Mock<IGuildUser> mockGuildUser = new Mock<IGuildUser>();
            mockGuildUser
                .Setup(user => user.Id)
                .Returns(userId);
            return mockGuildUser.Object;
        }

        private class MessageStore
        {
            public MessageStore()
            {
                this.DirectMessages = new List<string>();
            }

            public List<string> DirectMessages { get; }

            public void VerifyMessages(params string[] directMessages)
            {
                Assert.AreEqual(directMessages.Length, this.DirectMessages.Count, "Unexpected number of DMs.");
                for (int i = 0; i < directMessages.Length; i++)
                {
                    string message = directMessages[i];
                    Assert.AreEqual(message, this.DirectMessages[i], $"Unexpected DM at index {i}");
                }
            }
        }
    }
}
