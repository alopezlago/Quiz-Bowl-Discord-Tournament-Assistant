using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class TournamentDirectorCommandHandlerTests : CommandHandlerTestBase
    {
        const ulong DefaultAdminId = 321;
        const ulong DefaultUserId = 123;
        const string TeamName = "Team 1";

        [TestMethod]
        public async Task AddPlayerTeamDoesntExist()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.AddPlayer(guildUser, TeamName);
            string expectedMessage = string.Format(BotStrings.TeamDoesNotExist, TeamName);
            messageStore.VerifyDirectMessages(expectedMessage);

            // Tournament is no longer pending. Would need to use current tournament.
            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.IsFalse(
                    currentTournament.Players.Select(player => player.Id).Contains(DefaultUserId),
                    "Player was added."));
        }

        [TestMethod]
        public async Task AddPlayerAlreadyOnAnotherTeam()
        {
            const string otherTeamName = TeamName + "2";
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            state.AddTeams(new Team[] {
                mainTeam,
                new Team()
                {
                    Name = otherTeamName
                }
            });
            Assert.IsTrue(state.TryAddPlayer(new Player()
                {
                    Id = DefaultUserId,
                    Team = mainTeam
                }),
                "Adding the player the first time should succeed.");
            
            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.AddPlayer(guildUser, otherTeamName);
            string expectedMessage = string.Format(BotStrings.PlayerIsAlreadyOnTeam, guildUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                {
                    Player player = currentTournament.Players.First(p => p.Id == DefaultUserId);
                    Assert.AreEqual(mainTeam, player.Team, "Player's team was changed.");
                });
        }

        [TestMethod]
        public async Task AddPlayerSucceeds()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            state.AddTeams(new Team[] { mainTeam });
            
            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.AddPlayer(guildUser, TeamName);
            string expectedMessage = string.Format(BotStrings.AddPlayerSuccessful, guildUser.Mention, TeamName);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                {
                    Player player = currentTournament.Players.First(p => p.Id == DefaultUserId);
                    Assert.AreEqual(mainTeam, player.Team, "Player's team was set incorrectly.");
                });
        }

        [TestMethod]
        public async Task AddPlayersOnSameTeamSucceeds()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            state.AddTeams(new Team[] { mainTeam });

            for (ulong id = DefaultUserId; id <= DefaultUserId + 1; id++)
            {
                IGuildUser guildUser = CreateGuildUser(id);
                await commandHandler.AddPlayer(guildUser, TeamName);
                string expectedMessage = string.Format(BotStrings.AddPlayerSuccessful, guildUser.Mention, TeamName);
                messageStore.VerifyDirectMessages(expectedMessage);
                messageStore.Clear();
            }

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Player player = currentTournament.Players.First(p => p.Id == DefaultUserId);
                Assert.AreEqual(mainTeam, player.Team, "First player's team was set incorrectly.");
                Player secondPlayer = currentTournament.Players.First(p => p.Id == DefaultUserId + 1);
                Assert.AreEqual(mainTeam, player.Team, "Second player's team was set incorrectly.");
            });
        }

        [TestMethod]
        public async Task GetPlayersWithNoPlayers()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            await commandHandler.GetPlayers();
            messageStore.VerifyDirectMessages(BotStrings.NoTeamsYet);
        }

        [TestMethod]
        public async Task GetPlayers()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);
            Team firstTeam = new Team()
            {
                Name = "FirstTeam"
            };
            Team secondTeam = new Team()
            {
                Name = "SecondTeam"
            };
            state.AddTeams(new Team[] { firstTeam, secondTeam });

            Player[] players = new Player[3];
            for (ulong i = 1; i <= 3; i++)
            {
                Player player = new Player()
                {
                    Id = i,
                    Team = i == 3 ? secondTeam : firstTeam
                };
                IGuildUser guildUser = this.CreateGuildUser(i);
                Assert.IsTrue(state.TryAddPlayer(player), $"Could add player {player}");
            }

            await commandHandler.GetPlayers();
            string expectedMessage = $"FirstTeam: Nickname1, Nickname2{Environment.NewLine}SecondTeam: Nickname3";
            messageStore.VerifyDirectMessages(expectedMessage);
        }

        [TestMethod]
        public async Task RemovePlayerSucceeds()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            state.AddTeams(new Team[] { mainTeam });
            Assert.IsTrue(state.TryAddPlayer(new Player()
                {
                    Id = DefaultUserId,
                    Team = mainTeam
                }),
                "Adding the player the first time should succeed.");

            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.RemovePlayer(guildUser);
            string expectedMessage = string.Format(BotStrings.PlayerRemoved, guildUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.IsFalse(
                    currentTournament.Players.Any(), "Player should have been removed from the list of players."));
        }

        [TestMethod]
        public async Task RemovePlayerNotOnTeam()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            state.AddTeams(new Team[] { mainTeam });

            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.RemovePlayer(guildUser);
            string expectedMessage = string.Format(BotStrings.PlayerIsNotOnAnyTeam, guildUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.IsFalse(currentTournament.Players.Any(), "There should be no players in the list."));
        }

        [TestMethod]
        public async Task SetupFailsWithOngoingCurrentTournament()
        {
            const string tournamentName = DefaultTournamentName + "2";
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            ITournamentState newState = new TournamentState(DefaultGuildId, tournamentName);
            manager.AddOrUpdateTournament(tournamentName, newState, (name, oldState) => oldState);

            await commandHandler.Setup(tournamentName);
            string errorMessage = string.Format(
                TournamentStrings.TournamentAlreadyRunning, DefaultTournamentName);
            string expectedMessage = string.Format(
                BotStrings.ErrorSettingCurrentTournament, DefaultGuildName, errorMessage);
            messageStore.VerifyDirectMessages(expectedMessage);

            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.AreEqual(
                    DefaultTournamentName, currentTournament.Name, "Current tournament should remain the same."));
        }

        [TestMethod]
        public async Task SetupFailsUnknownTournament()
        {
            const string tournamentName = DefaultTournamentName + "2";
            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            await commandHandler.Setup(tournamentName);
            string errorMessage = string.Format(TournamentStrings.TournamentCannotBeFound, tournamentName);
            string expectedMessage = string.Format(
                BotStrings.ErrorSettingCurrentTournament, DefaultGuildName, errorMessage);
            messageStore.VerifyDirectMessages(expectedMessage);
        }

        [TestMethod]
        public async Task SetupSucceeds()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            IGuildUser adminUser = this.CreateGuildUser(DefaultAdminId);
            await commandHandler.AddTournamentDirector(adminUser, DefaultTournamentName);
            messageStore.Clear();

            await commandHandler.Setup(DefaultTournamentName);
            messageStore.VerifyChannelMessages();
            string expectedEmbed = this.GetMockEmbedText(
                TournamentStrings.AddReaders, TournamentStrings.ListMentionsOfAllReaders);
            messageStore.VerifyChannelEmbeds(expectedEmbed);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Assert.AreEqual(
                    DefaultTournamentName, currentTournament.Name, "Current tournament was not set correctly.");
                Assert.AreEqual(TournamentStage.AddReaders, currentTournament.Stage, "Wrong stage.");
            });
        }

        [TestMethod]
        public async Task SwitchPlayerTeamsPossible()
        {
            const string otherTeamName = TeamName + "2";
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            Team otherTeam = new Team()
            {
                Name = otherTeamName
            };
            state.AddTeams(new Team[] { mainTeam, otherTeam });

            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.AddPlayer(guildUser, TeamName);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                {
                    Player player = currentTournament.Players.First(p => p.Id == DefaultUserId);
                    Assert.AreEqual(mainTeam, player.Team, "Player's team was initially set incorrectly.");
                });

            await commandHandler.RemovePlayer(guildUser);
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.IsFalse(currentTournament.Players.Any(), "There should be no players in the list."));

            await commandHandler.AddPlayer(guildUser, otherTeamName);
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Player player = currentTournament.Players.First(p => p.Id == DefaultUserId);
                Assert.AreEqual(otherTeam, player.Team, "Player's team was not swapped correctly.");
            });
        }

        private static void VerifyOnCurrentTournament(TournamentsManager manager, Action<IReadOnlyTournamentState> action)
        {
            Assert.IsTrue(manager.TryReadActionOnCurrentTournament(action), "No current tournament to do read action.");
        }

        private void InitializeWithCurrentTournament(
            out MessageStore messageStore,
            out GlobalTournamentsManager globalManager,
            out BotCommandHandler commandHandler,
            out ITournamentState state)
        {
            messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore);
            globalManager = new GlobalTournamentsManager();
            commandHandler = new BotCommandHandler(context, globalManager);
            state = this.AddCurrentTournament(globalManager);
        }

        // start
        // switchReaders
        // Finals
        // end
    }
}
