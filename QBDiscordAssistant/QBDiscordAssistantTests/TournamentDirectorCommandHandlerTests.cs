using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class TournamentDirectorCommandHandlerTests : CommandHandlerTestBase
    {
        private const ulong DefaultAdminId = 321;
        private const ulong DefaultUserId = 123;
        private const string TeamName = "Team 1";

        [TestMethod]
        public async Task AddPlayerTeamDoesntExist()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.AddPlayerAsync(guildUser, TeamName);
            string expectedMessage = BotStrings.TeamDoesNotExist(TeamName);
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
            await commandHandler.AddPlayerAsync(guildUser, otherTeamName);
            string expectedMessage = BotStrings.PlayerIsAlreadyOnTeam(guildUser.Mention);
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
            await commandHandler.AddPlayerAsync(guildUser, TeamName);
            string expectedMessage = BotStrings.AddPlayerSuccessful(guildUser.Mention, TeamName);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                {
                    Player player = currentTournament.Players.First(p => p.Id == DefaultUserId);
                    Assert.AreEqual(mainTeam, player.Team, "Player's team was set incorrectly.");
                });
        }

        [TestMethod]
        public async Task AddPlayerAndRemovePlayerUpdateRoles()
        {
            const ulong teamRoleId = 2;
            this.InitializeWithCurrentTournament(
                new List<string>() { "Reader_Room_1", $"Team_{TeamName}" },
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            state.AddTeams(new Team[] { mainTeam });

            Reader reader = new Reader()
            {
                Id = 3,
                Name = "Reader"
            };

            // TournamentRoles is initialized in Setup in the command handler, but to avoid all that work we create
            // it directly here.
            state.TournamentRoles = new TournamentRoleIds(
                0,
                new KeyValuePair<Reader, ulong>[] { new KeyValuePair<Reader, ulong>(reader, 1) },
                new KeyValuePair<Team, ulong>[] { new KeyValuePair<Team, ulong>(mainTeam, teamRoleId) });
            state.UpdateStage(TournamentStage.RunningTournament, out string nextTitle, out string nextStageInstructions);

            IGuildUser guildUser = this.CreateGuildUser(DefaultUserId);
            await commandHandler.AddPlayerAsync(guildUser, TeamName);
            string expectedMessage = BotStrings.AddPlayerSuccessful(guildUser.Mention, TeamName);
            messageStore.VerifyDirectMessages(expectedMessage);
            messageStore.Clear();

            Assert.AreEqual(1, guildUser.RoleIds.Count, "Unexpected number of role IDs for added player");
            Assert.AreEqual(teamRoleId, guildUser.RoleIds.First(), "Unexpected role ID");

            await commandHandler.RemovePlayerAsync(guildUser);
            expectedMessage = BotStrings.PlayerRemoved(guildUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            Assert.AreEqual(0, guildUser.RoleIds.Count, "Unexpected number of role IDs for removed player");
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
                IGuildUser guildUser = this.CreateGuildUser(id);
                await commandHandler.AddPlayerAsync(guildUser, TeamName);
                string expectedMessage = BotStrings.AddPlayerSuccessful(guildUser.Mention, TeamName);
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
        public async Task BackOnAddReadersSucceeds()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out _,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            state.AddReaders(
                new Reader[] 
                { new Reader()
                    {
                        Id = 1,
                        Name = "Alice"
                    }
                });
            state.UpdateStage(TournamentStage.SetRoundRobins, out _, out _);

            Assert.AreEqual(
                1, state.Readers.Count(), "Unexpected number of readers after going to the next round");
            Assert.AreEqual(
                TournamentStage.SetRoundRobins, state.Stage, "Stage didn't go to setting the number of round robins");

            await commandHandler.GoBackAsync();
            Assert.AreEqual(TournamentStage.AddReaders, state.Stage, "Stage didn't go back to add readers");
            Assert.AreEqual(0, state.Readers.Count(), "Readers weren't cleared");
            Assert.AreEqual(0, messageStore.DirectMessages.Count, "No direct messages should've been sent");
        }

        [TestMethod]
        public async Task BackOnAddTeamsSucceeds()
        {
            const int roundRobinsCount = 2;
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out _,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            state.RoundRobinsCount = roundRobinsCount;
            state.UpdateStage(TournamentStage.AddTeams, out _, out _);

            Assert.AreEqual(
                roundRobinsCount, 
                state.RoundRobinsCount, 
                "Unexpected number of round robins after going to the next round");
            Assert.AreEqual(
                TournamentStage.AddTeams, state.Stage, "Stage didn't go to adding teams");

            await commandHandler.GoBackAsync();
            Assert.AreEqual(
                TournamentStage.SetRoundRobins, 
                state.Stage, 
                "Stage didn't go back to setting the number of round robins");
            Assert.AreEqual(0, state.RoundRobinsCount, "Round robins count wasn't reset");
            Assert.AreEqual(0, messageStore.DirectMessages.Count, "No direct messages should've been sent");
        }

        [TestMethod]
        public async Task BackOnAddPlayersSucceeds()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out _,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            Team[] teams = new Team[]
            {
                new Team()
                {
                    Bracket = 0,
                    Name = "A"
                },
                new Team()
                {
                    Bracket = 0,
                    Name = "B"
                },
            };

            state.AddTeams(teams);
            state.UpdateStage(TournamentStage.AddPlayers, out _, out _);

            Assert.AreEqual(
                teams.Length,
                state.Teams.Count(),
                "Unexpected number of teams after going to the next round");
            Assert.AreEqual(
                TournamentStage.AddPlayers, state.Stage, "Stage didn't go to adding players");

            await commandHandler.GoBackAsync();
            Assert.AreEqual(
                TournamentStage.AddTeams,
                state.Stage,
                "Stage didn't go back to adding teams");
            Assert.AreEqual(0, state.Teams.Count(), "Teams weren't reset");
            Assert.AreEqual(0, messageStore.DirectMessages.Count, "No direct messages should've been sent");
        }

        [TestMethod]
        public async Task BackOnFinalsFails()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out _,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            state.UpdateStage(TournamentStage.Finals, out _, out _);

            await commandHandler.GoBackAsync();
            Assert.AreEqual(TournamentStage.Finals, state.Stage, "Stage shouldn't of changed");
            messageStore.VerifyDirectMessages(BotStrings.CannotGoBack(TournamentStage.Finals));
        }

        [TestMethod]
        public async Task BackOnRebracketingSucceeds()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out _,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            state.UpdateStage(TournamentStage.Rebracketing, out _, out _);
            Assert.AreEqual(
                TournamentStage.Rebracketing, state.Stage, "Stage didn't go to rebracketing");

            await commandHandler.GoBackAsync();
            Assert.AreEqual(
                TournamentStage.RunningTournament,
                state.Stage,
                "Stage didn't go back to adding teams");
            Assert.AreEqual(0, messageStore.DirectMessages.Count, "No direct messages should've been sent");
            Assert.AreEqual(0, messageStore.ChannelEmbeds.Count, "No channel embeds should've been sent");
        }

        [TestMethod]
        public async Task BackSucceeds()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            TournamentStage[] stagesThatSupportBack = new TournamentStage[]
            {
                TournamentStage.AddReaders,
                TournamentStage.SetRoundRobins,
                TournamentStage.AddTeams,
                TournamentStage.AddPlayers,
                TournamentStage.BotSetup,
                TournamentStage.RunningTournament,
                TournamentStage.Rebracketing,
                TournamentStage.Finals
            };

            Team mainTeam = new Team()
            {
                Name = TeamName
            };
            state.AddTeams(new Team[] { mainTeam });

            for (ulong id = DefaultUserId; id <= DefaultUserId + 1; id++)
            {
                IGuildUser guildUser = this.CreateGuildUser(id);
                await commandHandler.AddPlayerAsync(guildUser, TeamName);
                string expectedMessage = BotStrings.AddPlayerSuccessful(guildUser.Mention, TeamName);
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
        public async Task EndSucceedsBeforeStart()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);

            state.UpdateStage(TournamentStage.AddReaders, out string nextStageTitle, out string nextStageInstructions);

            await commandHandler.EndTournamentAsync();
            messageStore.VerifyDirectMessages(BotStrings.TournamentCleanupFinished(DefaultGuildName));

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            Assert.IsFalse(
                manager.TryReadActionOnCurrentTournament((s) => { }),
                "There should be no current tournament to act on.");
        }

        [TestMethod]
        public async Task EndSucceedsWhenRunningTournament()
        {
            List<ulong> channelIds = new List<ulong>() { 0, 1 };
            const ulong directorId = 2;
            ulong[] readerRoleIds = new ulong[] { 3, 4 };
            ulong[] teamRoleIds = new ulong[] { 5, 6, 7, 8 };

            MessageStore messageStore = new MessageStore();
            Mock<IUser> mockUser = this.CreateMockUser(messageStore);
            Mock<IMessageChannel> mockMessageChannel = this.CreateMockMessageChannel(messageStore);
            Mock<IDiscordClient> mockClient = this.CreateMockClient();
            Mock<IGuild> mockGuild = this.CreateMockGuild(roleNames: new List<string>());

            HashSet<ulong> removedChannelIds = new HashSet<ulong>();
            HashSet<ulong> removedRoleIds = new HashSet<ulong>();
            mockGuild
                .Setup(guild => guild.GetChannelAsync(It.IsAny<ulong>(), It.IsAny<CacheMode>(), It.IsAny<RequestOptions>()))
                .Returns<ulong, CacheMode, RequestOptions>((id, cacheMode, options) =>
                {
                    Mock<IGuildChannel> mockChannel = new Mock<IGuildChannel>();
                    mockChannel
                        .Setup(channel => channel.DeleteAsync(It.IsAny<RequestOptions>()))
                        .Returns<RequestOptions>((channelRequestOptions) =>
                        {
                            Assert.IsFalse(removedChannelIds.Contains(id), $"Channel was removed twice: {id}");
                            removedChannelIds.Add(id);
                            return Task.CompletedTask;
                        });
                    return Task.FromResult(mockChannel.Object);
                });
            mockGuild
                .Setup(guild => guild.GetRole(It.IsAny<ulong>()))
                .Returns<ulong>(id =>
                {
                    Mock<IRole> mockRole = new Mock<IRole>();
                    mockRole
                        .Setup(role => role.DeleteAsync(It.IsAny<RequestOptions>()))
                        .Returns<RequestOptions>((options) =>
                        {
                            Assert.IsFalse(removedRoleIds.Contains(id), $"Role was removed twice: {id}");
                            removedRoleIds.Add(id);
                            return Task.CompletedTask;
                        });
                    return mockRole.Object;
                });

            ICommandContext context = this.CreateMockCommandContext(
                mockGuild, mockMessageChannel, mockClient, mockUser)
                .Object;

            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            ITournamentState state = this.AddCurrentTournament(globalManager);

            state.UpdateStage(
                TournamentStage.RunningTournament, out string nextStageTitle, out string nextStageInstructions);

            Team[] teams = teamRoleIds
                .Select(id => new Team() { Name = $"Team {id}" })
                .ToArray();
            state.AddTeams(teams);

            Reader[] readers = readerRoleIds
                .Select(id => new Reader() { Id = id, Name = $"Reader {id}" })
                .ToArray();
            state.AddReaders(readers);

            Dictionary<Team, ulong> teamRoleIdMap = new Dictionary<Team, ulong>();
            for (int i = 0; i < teamRoleIds.Length; i++)
            {
                teamRoleIdMap[teams[i]] = teamRoleIds[i];
            }

            Dictionary<Reader, ulong> readerRoleIdMap = new Dictionary<Reader, ulong>();
            for (int i = 0; i < readerRoleIds.Length; i++)
            {
                readerRoleIdMap[readers[i]] = readerRoleIds[i];
            }

            state.ChannelIds = channelIds;
            state.TournamentRoles = new TournamentRoleIds(directorId, readerRoleIdMap, teamRoleIdMap);

            await commandHandler.EndTournamentAsync();
            messageStore.VerifyDirectMessages(BotStrings.TournamentCleanupFinished(DefaultGuildName));

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            Assert.IsFalse(
                manager.TryReadActionOnCurrentTournament((s) => { }),
                "There should be no current tournament to act on.");
            Assert.AreEqual(TournamentStage.Complete, state.Stage, "Unexpected end stage.");

            Assert.AreEqual(2, removedChannelIds.Count, "Wrong number of channels deleted.");
            foreach (ulong id in channelIds)
            {
                Assert.IsTrue(removedChannelIds.Contains(id), $"Channel ID {id} was not removed.");
            }

            Assert.AreEqual(
                teamRoleIds.Length + readerRoleIds.Length + 1, removedRoleIds.Count, "Wrong number of roles removed.");
            foreach (ulong id in teamRoleIds.Concat(readerRoleIds).Concat(new ulong[] { directorId }))
            {
                Assert.IsTrue(removedRoleIds.Contains(id), $"Role ID {id} was not removed.");
            }
        }

        [TestMethod]
        public async Task FinalsFailsWhenNotRunningTournament()
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
            await VerifyAllowedStages(
                state,
                messageStore,
                new HashSet<TournamentStage>()
                {
                    TournamentStage.RunningTournament
                },
                BotStrings.ErrorFinalsOnlySetDuringPrelimsOrPlayoffs,
                () => commandHandler.SetupFinalsAsync(readerUser, rawTeamNameParts));
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
                BotStrings.ErrorAtLeastOneTeamNotInTournament(rawTeamNameParts));
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
                BotStrings.ErrorTwoTeamsMustBeSpecifiedFinals(teams.Length));
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
                BotStrings.ErrorTwoTeamsMustBeSpecifiedFinals(1));
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
                1,
                new KeyValuePair<Reader, ulong>[]
                {
                    new KeyValuePair<Reader, ulong>(reader, 2)
                },
                new KeyValuePair<Team, ulong>[]
                {
                    new KeyValuePair<Team, ulong>(firstTeam, 3),
                    new KeyValuePair<Team, ulong>(secondTeam, 4)
                });
            state.ChannelIds = new ulong[] { 10, 11 };

            IGuildUser readerUser = this.CreateGuildUser(DefaultUserId, new List<string>(roles));
            state.UpdateStage(TournamentStage.RunningTournament, out string nextTitle, out string nextStageInstructions);
            await commandHandler.SetupFinalsAsync(readerUser, rawTeamNameParts);
            messageStore.VerifyDirectMessages();

            string expectedMessage = BotStrings.FinalsParticipantsPleaseJoin($"@{finalsChannelName}");
            messageStore.VerifyChannelMessages(expectedMessage);

            Assert.AreEqual(TournamentStage.Finals, state.Stage, "Stage should have been changed.");

            // First channel is the category. Second is the text channel.
            IGuildChannel categoryChannel = await context.Guild.GetChannelAsync(0);
            Assert.AreEqual("Finals", categoryChannel.Name, "Unexpected name for finals category channel.");

            IGuildChannel channel = await context.Guild.GetChannelAsync(1);
            Assert.AreEqual(finalsChannelName, channel.Name, "Unexpected name for finals channel.");
            foreach (IRole r in context.Guild.Roles)
            {
                OverwritePermissions? overwritePermissions = channel.GetPermissionOverwrite(r);
                OverwritePermissions? expectedPermissions = null;
                switch (r.Name)
                {
                    case directorRole:
                        expectedPermissions = TournamentChannelManager.PrivilegedOverwritePermissions;
                        break;
                    case readerRole:
                        expectedPermissions = TournamentChannelManager.PrivilegedOverwritePermissions;
                        break;
                    case team1Role:
                        expectedPermissions = TournamentChannelManager.TeamPermissions;
                        break;
                    case team2Role:
                        expectedPermissions = TournamentChannelManager.TeamPermissions;
                        break;
                    default:
                        Assert.Fail($"Unexpected role created: {r.Name}");
                        break;
                }

                Assert.AreEqual(
                    expectedPermissions, overwritePermissions, $"Unexpected permssions for {r.Name}");
            }

            Assert.AreEqual(
                TournamentChannelManager.PrivilegedOverwritePermissions,
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

            await commandHandler.GetPlayersAsync();
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

            await commandHandler.GetPlayersAsync();
            string expectedMessage = $"FirstTeam: Nickname1, Nickname2{Environment.NewLine}SecondTeam: Nickname3";
            messageStore.VerifyDirectMessages(expectedMessage);
        }

        [TestMethod]
        public async Task RebracketFailsWhenNotRunningTournament()
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

            await VerifyAllowedStages(
                state,
                messageStore,
                new HashSet<TournamentStage>()
                {
                    TournamentStage.RunningTournament
                },
                BotStrings.CanOnlyRebracketWhileRunning,
                () => commandHandler.RebracketAsync());
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
            await commandHandler.RemovePlayerAsync(guildUser);
            string expectedMessage = BotStrings.PlayerRemoved(guildUser.Mention);
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
            await commandHandler.RemovePlayerAsync(guildUser);
            string expectedMessage = BotStrings.PlayerIsNotOnAnyTeam(guildUser.Mention);
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

            await commandHandler.SetupTournamentAsync(tournamentName);
            string errorMessage = TournamentStrings.TournamentAlreadyRunning(DefaultTournamentName);
            string expectedMessage = BotStrings.ErrorSettingCurrentTournament(DefaultGuildName, errorMessage);
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

            await commandHandler.SetupTournamentAsync(tournamentName);
            string errorMessage = TournamentStrings.TournamentCannotBeFound(tournamentName);
            string expectedMessage = BotStrings.ErrorSettingCurrentTournament(DefaultGuildName, errorMessage);
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
            await commandHandler.AddTournamentDirectorAsync(adminUser, DefaultTournamentName);
            messageStore.Clear();

            await commandHandler.SetupTournamentAsync(DefaultTournamentName);
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
        public async Task StartFailsWhenNotInAddPlayers()
        {
            this.InitializeWithCurrentTournament(
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state);
            await VerifyAllowedStages(
                state,
                messageStore,
                new HashSet<TournamentStage>()
                {
                    TournamentStage.AddPlayers
                },
                BotStrings.CommandOnlyUsedTournamentReadyStart,
                () => commandHandler.StartAsync());
        }

        [TestMethod]
        public async Task StartRollsBackOnException()
        {
            this.InitializeWithCurrentTournament(
                new List<string>(),
                out MessageStore messageStore,
                out GlobalTournamentsManager globalManager,
                out BotCommandHandler commandHandler,
                out ITournamentState state,
                out ICommandContext context);

            state.UpdateStage(TournamentStage.AddPlayers, out string nextTitle, out string nextInstructions);

            bool exceptionThrown = false;
            try
            {
                // Start should throw because there are no teams or readers.
                await commandHandler.StartAsync();
            }
            catch (Exception)
            {
                exceptionThrown = true;
            }

            Assert.IsTrue(exceptionThrown, "Exception was not thrown.");

            // TODO: Consider verifying messages. We do get a message about cleanup and completing the tournament,
            // which isn't accurate.

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.AreEqual(TournamentStage.AddPlayers, currentTournament.Stage, "Wrong stage."));
            IReadOnlyCollection<IGuildChannel> createdChannels = await context.Guild.GetChannelsAsync();
            Assert.AreEqual(0, createdChannels.Count, "All channels should be cleaned up.");
            Assert.AreEqual(0, context.Guild.Roles.Count, "All roles should be cleaned up.");
        }

        [TestMethod]
        public async Task StartSuceeds()
        {
            const string readerName = "Only Reader";
            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = readerName
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
            Team[] teams = new Team[] { firstTeam, secondTeam };

            List<Player> players = new List<Player>();
            for (int i = 0; i < 3; i++)
            {
                players.Add(new Player()
                {
                    Id = (ulong)(90 + i),
                    Team = i % 2 == 0 ? firstTeam : secondTeam
                });
            }

            MessageStore messageStore = new MessageStore();

            Mock<IUser> mockUser = this.CreateMockUser(messageStore);
            Mock<IMessageChannel> mockMessageChannel = this.CreateMockMessageChannel(messageStore);
            Mock<IDiscordClient> mockClient = this.CreateMockClient();
            Mock<IGuild> mockGuild = this.CreateMockGuild(roleNames: new List<string>());
            mockGuild
                .Setup(guild => guild.GetUsersAsync(It.IsAny<CacheMode>(), null))
                .Returns<CacheMode, RequestOptions>((mode, options) =>
                {
                    List<IGuildUser> guildUsers = new List<IGuildUser>
                    {
                        this.CreateGuildUser(reader.Id)
                    };
                    guildUsers.AddRange(players.Select(player => this.CreateGuildUser(player.Id)));

                    IReadOnlyCollection<IGuildUser> readOnlyGuildUsers = ImmutableArray.Create(guildUsers.ToArray());
                    return Task.FromResult(readOnlyGuildUsers);
                });

            ICommandContext context = this.CreateMockCommandContext(
                mockGuild, mockMessageChannel, mockClient, mockUser)
                .Object;

            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);
            ITournamentState state = this.AddCurrentTournament(globalManager);

            IGuildUser adminUser = this.CreateGuildUser(DefaultAdminId);
            await commandHandler.AddTournamentDirectorAsync(adminUser, DefaultTournamentName);
            messageStore.Clear();

            state.UpdateStage(TournamentStage.AddPlayers, out string nextTitle, out string nextInstructions);

            state.AddReaders(readers);
            state.AddTeams(teams);

            state.RoundRobinsCount = 1;

            state.TournamentRoles = new TournamentRoleIds(
                0,
                new KeyValuePair<Reader, ulong>[] { new KeyValuePair<Reader, ulong>(reader, 1) },
                new KeyValuePair<Team, ulong>[]
                {
                    new KeyValuePair<Team, ulong>(firstTeam, 2),
                    new KeyValuePair<Team, ulong>(secondTeam, 3)
                });

            await commandHandler.StartAsync();

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.AreEqual(TournamentStage.RunningTournament, currentTournament.Stage, "Wrong stage."));
            IReadOnlyCollection<IGuildChannel> createdChannels = await context.Guild.GetChannelsAsync();

            // TODO: Find a way to assert permissions and channel type.
            IDictionary<string, IGuildChannel> createdChannelsMap = createdChannels.ToDictionary(guild => guild.Name);
            Assert.IsTrue(
                createdChannelsMap.TryGetValue("Round 1", out IGuildChannel roundCategoryChannel),
                "Round channel is missing.");
            Assert.IsTrue(
                createdChannelsMap.TryGetValue("Readers", out IGuildChannel readersCategoryChannel),
                "Readers channel is missing.");
            Assert.IsTrue(
                createdChannelsMap.TryGetValue(
                    $"Round_1_{readerName.Replace(" ", "_")}", out IGuildChannel roundOneTextChannel),
                "Round 1 text channel channel is missing.");
            Assert.IsTrue(
                createdChannelsMap.TryGetValue(
                    $"{readerName.Replace(" ", "_")}'s_Voice_Channel", out IGuildChannel roundOneVoiceChannel),
                "Round 1 voice channel channel is missing.");
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
            await commandHandler.AddPlayerAsync(guildUser, TeamName);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                {
                    Player player = currentTournament.Players.First(p => p.Id == DefaultUserId);
                    Assert.AreEqual(mainTeam, player.Team, "Player's team was initially set incorrectly.");
                });

            await commandHandler.RemovePlayerAsync(guildUser);
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.IsFalse(currentTournament.Players.Any(), "There should be no players in the list."));

            await commandHandler.AddPlayerAsync(guildUser, otherTeamName);
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

            await VerifyAllowedStages(
                state,
                messageStore,
                new HashSet<TournamentStage>()
                {
                    TournamentStage.RunningTournament,
                    TournamentStage.Rebracketing,
                    TournamentStage.Finals,
                    TournamentStage.Complete
                },
                BotStrings.CommandOnlyUsedWhileTournamentRunning,
                () => commandHandler.SwitchReaderAsync(oldReaderUser, newReaderUser));
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

            Reader reader = new Reader()
            {
                Id = DefaultUserId,
                Name = "Name"
            };
            state.AddReaders(new Reader[] { reader });

            // TournamentRoles is initialized in Setup in the command handler, but to avoid all that work we create
            // it directly here.
            state.TournamentRoles = new TournamentRoleIds(
                0,
                new KeyValuePair<Reader, ulong>[] { new KeyValuePair<Reader, ulong>(reader, 1) },
                Enumerable.Empty<KeyValuePair<Team, ulong>>());

            state.UpdateStage(TournamentStage.RunningTournament, out string nextTitle, out string nextStageInstructions);

            IGuildUser oldReaderUser = this.CreateGuildUser(DefaultUserId + 1);
            IGuildUser newReaderUser = this.CreateGuildUser(DefaultUserId + 2, new List<string>(readerRoles));
            await commandHandler.SwitchReaderAsync(oldReaderUser, newReaderUser);
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

            // TournamentRoles is initialized in Setup in the command handler, but to avoid all that work we create
            // it directly here.
            state.TournamentRoles = new TournamentRoleIds(
                0,
                new KeyValuePair<Reader, ulong>[]
                {
                    new KeyValuePair<Reader, ulong>(firstReader, 1),
                    new KeyValuePair<Reader, ulong>(secondReader, 2)
                },
                Enumerable.Empty<KeyValuePair<Team, ulong>>());

            state.UpdateStage(TournamentStage.RunningTournament, out string nextTitle, out string nextStageInstructions);

            IGuildUser oldReaderUser = this.CreateGuildUser(DefaultUserId, new List<string>(readerRoles));
            IGuildUser newReaderUser = this.CreateGuildUser(DefaultUserId + 1, new List<string>(readerRoles));
            await commandHandler.SwitchReaderAsync(oldReaderUser, newReaderUser);
            string expectedMessage = BotStrings.IsAlreadyReader(newReaderUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Assert.IsTrue(currentTournament.Readers.Contains(firstReader), "First reader was removed.");
                Assert.IsTrue(currentTournament.Readers.Contains(secondReader), "Second reader was removed.");
            });
        }

        [TestMethod]
        public async Task SwitchReadersSucceeds()
        {
            const string role = "Reader_Room_1";
            const ulong readerRoleId = 1;
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
            state.AddReaders(new Reader[] { firstReader });

            // TournamentRoles is initialized in Setup in the command handler, but to avoid all that work we create
            // it directly here.
            state.TournamentRoles = new TournamentRoleIds(
                0,
                new KeyValuePair<Reader, ulong>[] { new KeyValuePair<Reader, ulong>(firstReader, readerRoleId) },
                Enumerable.Empty<KeyValuePair<Team, ulong>>());

            state.UpdateStage(TournamentStage.RunningTournament, out string nextTitle, out string nextStageInstructions);

            IGuildUser oldReaderUser = this.CreateGuildUser(DefaultUserId, new List<string>(readerRoles));
            IGuildUser newReaderUser = this.CreateGuildUser(DefaultUserId + 1);
            await commandHandler.SwitchReaderAsync(oldReaderUser, newReaderUser);
            messageStore.VerifyDirectMessages(BotStrings.ReadersSwitchedSuccessfully);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
            {
                Assert.AreEqual(1, currentTournament.Readers.Count(), "Unexpected number of readers.");
                Assert.AreEqual(DefaultUserId + 1, currentTournament.Readers.First().Id, "Reader was not swapped.");
                Assert.AreEqual(0, oldReaderUser.RoleIds.Count, "Old reader should no longer have reader role.");
                Assert.AreEqual(1, newReaderUser.RoleIds.Count, "New reader should have a role.");
                Assert.AreEqual(readerRoleId, newReaderUser.RoleIds.First(), "New reader should have reader role.");
            });
        }

        [TestMethod]
        public async Task SwitchReadersSameReader()
        {
            const string role = "Reader_Room_1";
            const ulong readerRoleId = 1;
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

            // TournamentRoles is initialized in Setup in the command handler, but to avoid all that work we create
            // it directly here.
            state.TournamentRoles = new TournamentRoleIds(
                0,
                new KeyValuePair<Reader, ulong>[] { new KeyValuePair<Reader, ulong>(reader, readerRoleId) },
                Enumerable.Empty<KeyValuePair<Team, ulong>>());

            state.UpdateStage(TournamentStage.RunningTournament, out string nextTitle, out string nextStageInstructions);

            IGuildUser readerUser = this.CreateGuildUser(DefaultUserId, new List<string>(readerRoles));
            await commandHandler.SwitchReaderAsync(readerUser, readerUser);
            string expectedMessage = BotStrings.IsAlreadyReader(readerUser.Mention);
            messageStore.VerifyDirectMessages(expectedMessage);

            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());
            VerifyOnCurrentTournament(manager, currentTournament =>
                Assert.IsTrue(currentTournament.Readers.Contains(reader), "First reader was removed."));
        }

        private static async Task VerifyAllowedStages(
            ITournamentState state,
            MessageStore messageStore,
            ISet<TournamentStage> allowedStages,
            string expectedErrorMessage,
            Func<Task> action)
        {
            foreach (TournamentStage stage in Enum.GetValues(typeof(TournamentStage)))
            {
                if (allowedStages.Contains(stage))
                {
                    continue;
                }

                state.UpdateStage(stage, out string _, out string _);
                await action();
                messageStore.VerifyDirectMessages(expectedErrorMessage);
                messageStore.Clear();
                Assert.AreEqual(stage, state.Stage, "Stage should not have been changed.");
            }
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
            state.UpdateStage(TournamentStage.RunningTournament, out string nextTitle, out string nextStageInstructions);
            await commandHandler.SetupFinalsAsync(readerUser, rawTeamNameParts);
            messageStore.VerifyDirectMessages(expectedErrorMessage);

            Assert.AreEqual(TournamentStage.RunningTournament, state.Stage, "Stage should not have been changed.");
        }

        // start
        // end
    }
}
