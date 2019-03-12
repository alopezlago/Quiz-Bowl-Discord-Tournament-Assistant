using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;
using System;
using System.Collections.Generic;
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
        public async Task FinalsFailsWhenNotRunningPrelims()
        {
            this.InitializeWithCurrentTournament(   
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };
            state.AddReaders(new Reader[] { reader });

            Team firstTeam = new Team()
            {
                Name = "Team1"
            };
            Team secondTeam = new Team()
            {
                Name = "Team2"
            };
            state.AddTeams(new Team[] { firstTeam, secondTeam });

            IGuildUser readerUser = this.CreateGuildUser(DefaultUserId);
            string rawTeamNameParts = "Team1, Team2";
            foreach(TournamentStage stage in Enum.GetValues(typeof(TournamentStage)))
            {
                if (stage == TournamentStage.RunningPrelims)
                {
                    continue;
                }

                state.UpdateStage(stage, out string nextTitle, out string nextStageInstructions);
                await commandHandler.Finals(readerUser, rawTeamNameParts);
                messageStore.VerifyDirectMessages(BotStrings.ErrorFinalsOnlySetDuringPrelims);
                messageStore.Clear();
                Assert.AreEqual(stage, state.Stage, "Stage should not have been changed.");
            }
        }

        [TestMethod]
        public async Task FinalsFailsWithNonReader()
        {
            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };

            Team firstTeam = new Team()
            {
                Name = "Team1"
            };
            Team secondTeam = new Team()
            {
                Name = "Team2"
            };

            await this.VerifyFinalsFails(
                new Reader[] { reader },
                new Team[] { firstTeam, secondTeam },
                DefaultUserId + 1,
                "Team1, Team2",
                BotStrings.ErrorGivenUserIsntAReader);
        }

        [TestMethod]
        public async Task FinalsFailsWithOneNonexistentTeam()
        {
            const string nonexistentTeam = "Team3";
            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };

            Team firstTeam = new Team()
            {
                Name = "Team1"
            };
            Team secondTeam = new Team()
            {
                Name = "Team2"
            };

            string rawTeamNameParts = $"Team1, {nonexistentTeam}";
            await this.VerifyFinalsFails(
                new Reader[] { reader },
                new Team[] { firstTeam, secondTeam },
                DefaultUserId,
                rawTeamNameParts,
                string.Format(BotStrings.ErrorAtLeastOneTeamNotInTournament, rawTeamNameParts));
        }

        [TestMethod]
        public async Task FinalsFailsWithMoreThanTwoTeams()
        {
            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };

            Team firstTeam = new Team()
            {
                Name = "Team1"
            };
            Team secondTeam = new Team()
            {
                Name = "Team2"
            };
            Team thirdTeam = new Team()
            {
                Name = "Team3"
            };
            Team[] teams = new Team[] { firstTeam, secondTeam, thirdTeam };

            string rawTeamNameParts = "Team1, Team2, Team3";
            await this.VerifyFinalsFails(
                new Reader[] { reader },
                teams,
                DefaultUserId,
                rawTeamNameParts,
                string.Format(BotStrings.ErrorTwoTeamsMustBeSpecifiedFinals, teams.Length));
        }

        [TestMethod]
        public async Task FinalsFailsWithNullTeams()
        {
            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };

            Team firstTeam = new Team()
            {
                Name = "Team1"
            };
            Team secondTeam = new Team()
            {
                Name = "Team2"
            };
            
            await this.VerifyFinalsFails(
                new Reader[] { reader },
                new Team[] { firstTeam, secondTeam },
                DefaultUserId,
                null,
                BotStrings.ErrorNoTeamsSpecified);
        }

        [TestMethod]
        public async Task FinalsFailsWithSameTeams()
        {
            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };

            Team firstTeam = new Team()
            {
                Name = "Team1"
            };
            Team secondTeam = new Team()
            {
                Name = "Team3"
            };

            string rawTeamNameParts = "Team1, Team1";
            await this.VerifyFinalsFails(
                new Reader[] { reader },
                new Team[] { firstTeam, secondTeam },
                DefaultUserId,
                rawTeamNameParts,
                string.Format(BotStrings.ErrorTwoTeamsMustBeSpecifiedFinals, 1));
        }

        [TestMethod]
        public async Task FinalsSucceeds()
        {
            const string rawTeamNameParts = "Team1, Team2";
            const string directorRole = "Director";
            const string readerRole = "Reader_Room_1";
            const string team1Role = "Team_1";
            const string team2Role = "Team_2";
            const string finalsChannelName = "Round_4_Only_Reader";

            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "Only Reader"
            };
            Reader[] readers = new Reader[] { reader };

            Team firstTeam = new Team()
            {
                Name = "Team1"
            };
            Team secondTeam = new Team()
            {
                Name = "Team2"
            };
            Team thirdTeam = new Team()
            {
                Name = "Team3"
            };
            Team[] teams = new Team[] { firstTeam, secondTeam, thirdTeam };

            List<string> roles = new List<string>(new string[] { directorRole, readerRole, team1Role, team2Role });
            this.InitializeWithCurrentTournament(
                roles,
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state,
                out ICommandContext context);

            state.AddReaders(readers);
            state.AddTeams(teams);
            state.Schedule = new RoundRobinScheduleFactory(1)
                .Generate(new HashSet<Team>(teams), new HashSet<Reader>(readers));
            state.TournamentRoles = new TournamentRoleIds(
                0, 
                new ulong[] { 1 }, 
                new KeyValuePair<Team, ulong>[]
                {
                    new KeyValuePair<Team, ulong>(firstTeam, 2),
                    new KeyValuePair<Team, ulong>(secondTeam, 3)
                });

            IGuildUser readerUser = this.CreateGuildUser(DefaultUserId, new List<string>(roles));
            state.UpdateStage(TournamentStage.RunningPrelims, out string nextTitle, out string nextStageInstructions);
            await commandHandler.Finals(readerUser, rawTeamNameParts);
            messageStore.VerifyDirectMessages();

            string expectedMessage = string.Format(BotStrings.FinalsParticipantsPleaseJoin, $"@{finalsChannelName}");
            messageStore.VerifyChannelMessages(expectedMessage);

            Assert.AreEqual(TournamentStage.Finals, state.Stage, "Stage should have been changed.");

            IGuildChannel channel = await context.Guild.GetChannelAsync(0);
            Assert.AreEqual(finalsChannelName, channel.Name, "Unexpected name for finals channel.");
            foreach (IRole r in context.Guild.Roles)
            {
                OverwritePermissions? overwritePermissions = channel.GetPermissionOverwrite(r);
                OverwritePermissions? expectedPermissions = null;
                switch (r.Name)
                {
                    case directorRole:
                        expectedPermissions = BotCommandHandler.PrivilegedOverwritePermissions;
                        break;
                    case readerRole:
                        expectedPermissions = BotCommandHandler.PrivilegedOverwritePermissions;
                        break;
                    case team1Role:
                        expectedPermissions = BotCommandHandler.TeamPermissions;
                        break;
                    case team2Role:
                        expectedPermissions = BotCommandHandler.TeamPermissions;
                        break;
                    default:
                        Assert.Fail($"Unexpected role created: {r.Name}");
                        break;
                }

                Assert.AreEqual(
                    expectedPermissions, overwritePermissions, $"Unexpected permssions for {r.Name}");
            }

            Assert.AreEqual(
                BotCommandHandler.PrivilegedOverwritePermissions, 
                channel.GetPermissionOverwrite(context.Client.CurrentUser), 
                "Bot doesn't have the proper permissions.");
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

        [TestMethod]
        public async Task SwitchReadersFailsWhenTournamentHasntStarted()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            IGuildUser oldReaderUser = this.CreateGuildUser(DefaultUserId);
            IGuildUser newReaderUser = this.CreateGuildUser(DefaultUserId + 1);

            for (TournamentStage stage = TournamentStage.Created; stage < TournamentStage.RunningPrelims; ++stage)
            {
                state.UpdateStage(stage, out string nextTitle, out string nextStageInstructions);
                await commandHandler.SwitchReader(oldReaderUser, newReaderUser);
                messageStore.VerifyDirectMessages(BotStrings.ThisCommandCanOnlyBeUsedWhileTournamentRunning);
                messageStore.Clear();
                Assert.AreEqual(stage, state.Stage, "Stage should not have been changed.");
            }
        }

        [TestMethod]
        public async Task SwitchReadersFailsWhenUserWasNotReader()
        {
            List<string> readerRoles = new List<string>(new string[] { "Reader_Room_" });
            this.InitializeWithCurrentTournament(
                readerRoles,
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            state.AddReaders(new Reader[] {
                new Reader()
                {
                    Id = DefaultUserId,
                    Name = "Name"
                }
            });

            state.UpdateStage(TournamentStage.RunningPrelims, out string nextTitle, out string nextStageInstructions);

            IGuildUser oldReaderUser = this.CreateGuildUser(DefaultUserId + 1, new List<string>());
            IGuildUser newReaderUser = this.CreateGuildUser(DefaultUserId + 2, new List<string>(readerRoles));
            await commandHandler.SwitchReader(oldReaderUser, newReaderUser);
            messageStore.VerifyDirectMessages(BotStrings.CouldntGetRoleForTheOldReader);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Assert.AreEqual(1, currentTournament.Readers.Count(), "Only one reader should exist.");
                Assert.AreEqual(
                    DefaultUserId, currentTournament.Readers.First().Id, "Reader should not have switched.");
            });
        }

        [TestMethod]
        public async Task SwitchReadersFailsWhenOtherUserIsReader()
        {
            List<string> readerRoles = new List<string>(new string[] { "Reader_Room_" });
            this.InitializeWithCurrentTournament(
                readerRoles,
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Reader firstReader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };
            Reader secondReader = new Reader()
            {
                Id = DefaultUserId + 1,
                Name = "Second Reader"
            };
            state.AddReaders(new Reader[] { firstReader, secondReader });

            state.UpdateStage(TournamentStage.RunningPrelims, out string nextTitle, out string nextStageInstructions);

            IGuildUser oldReaderUser = this.CreateGuildUser(DefaultUserId, new List<string>(readerRoles));
            IGuildUser newReaderUser = this.CreateGuildUser(DefaultUserId + 1, new List<string>(readerRoles));
            await commandHandler.SwitchReader(oldReaderUser, newReaderUser);
            string expectedMessage = string.Format(BotStrings.IsAlreadyReader, newReaderUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Assert.IsTrue(currentTournament.Readers.Contains(firstReader), "First reader was removed.");
                Assert.IsTrue(currentTournament.Readers.Contains(secondReader), "First reader was removed.");
            });
        }

        [TestMethod]
        public async Task SwitchReadersSucceeds()
        {
            const string role = "Reader_Room_1";
            List<string> readerRoles = new List<string>(new string[] { role });
            this.InitializeWithCurrentTournament(
                readerRoles,
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Reader firstReader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };
            state.AddReaders(new Reader[] { firstReader  });

            state.UpdateStage(TournamentStage.RunningPrelims, out string nextTitle, out string nextStageInstructions);

            IGuildUser oldReaderUser = this.CreateGuildUser(DefaultUserId, new List<string>(readerRoles));
            IGuildUser newReaderUser = this.CreateGuildUser(DefaultUserId + 1, new List<string>());
            await commandHandler.SwitchReader(oldReaderUser, newReaderUser);
            messageStore.VerifyDirectMessages(BotStrings.ReadersSwitchedSuccessfully);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Assert.AreEqual(1, currentTournament.Readers.Count(), "Unexpected number of readers.");
                Assert.AreEqual(DefaultUserId + 1, currentTournament.Readers.First().Id, "Reader was not swapped.");
                Assert.AreEqual(0, oldReaderUser.RoleIds.Count, "Old reader should no longer have reader role.");
                Assert.AreEqual(1, newReaderUser.RoleIds.Count, "New reader should have a role.");
                Assert.AreEqual(0u, newReaderUser.RoleIds.First(), "New reader should have reader role.");
            });
        }

        [TestMethod]
        public async Task SwitchReadersSameReader()
        {
            const string role = "Reader_Room_1";
            List<string> readerRoles = new List<string>(new string[] { role });
            this.InitializeWithCurrentTournament(
                readerRoles,
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "First Reader"
            };
            state.AddReaders(new Reader[] { reader });

            state.UpdateStage(TournamentStage.RunningPrelims, out string nextTitle, out string nextStageInstructions);

            IGuildUser readerUser = this.CreateGuildUser(DefaultUserId, new List<string>(readerRoles));
            await commandHandler.SwitchReader(readerUser, readerUser);
            string expectedMessage = string.Format(BotStrings.IsAlreadyReader, readerUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.IsTrue(currentTournament.Readers.Contains(reader), "First reader was removed."));
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
            this.InitializeWithCurrentTournament(
                null,
                out messageStore,
                out globalManager,
                out commandHandler,
                out state);
        }

        private void InitializeWithCurrentTournament(
            List<string> roleNames,
            out MessageStore messageStore,
            out GlobalTournamentsManager globalManager,
            out BotCommandHandler commandHandler,
            out ITournamentState state)
        {
            this.InitializeWithCurrentTournament(
                roleNames,
                out messageStore,
                out globalManager,
                out commandHandler,
                out state,
                out ICommandContext context);
        }
        
        private void InitializeWithCurrentTournament(
            List<string> roleNames,
            out MessageStore messageStore,
            out GlobalTournamentsManager globalManager,
            out BotCommandHandler commandHandler,
            out ITournamentState state,
            out ICommandContext context)
        {
            messageStore = new MessageStore();
            context = this.CreateCommandContext(messageStore, roleNames: roleNames);
            globalManager = new GlobalTournamentsManager();
            commandHandler = new BotCommandHandler(context, globalManager);
            state = this.AddCurrentTournament(globalManager);
        }

        private async Task VerifyFinalsFails(
            Reader[] readers,
            Team[] teams,
            ulong readerId,
            string rawTeamNameParts,
            string expectedErrorMessage)
        {
            const string role = "Reader_Room_1";
            this.InitializeWithCurrentTournament(
                new List<string>(new string[] { role }),
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            state.AddReaders(readers);
            state.AddTeams(teams);

            IGuildUser readerUser = this.CreateGuildUser(readerId);
            state.UpdateStage(TournamentStage.RunningPrelims, out string nextTitle, out string nextStageInstructions);
            await commandHandler.Finals(readerUser, rawTeamNameParts);
            messageStore.VerifyDirectMessages(expectedErrorMessage);

            Assert.AreEqual(TournamentStage.RunningPrelims, state.Stage, "Stage should not have been changed.");
        }

        // start
        // end
    }
}
