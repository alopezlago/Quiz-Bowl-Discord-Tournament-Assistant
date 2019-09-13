using Discord;
using Discord.Commands;
using QBDiscordAssistant.Tournament;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Game = QBDiscordAssistant.Tournament.Game;

[assembly: InternalsVisibleTo("QBDiscordAssistantTests")]

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class BotCommandHandler
    {
        private const string DirectorRoleName = "Director";
        private const string ReaderRoomRolePrefix = "Reader_Room_";
        private const string TeamRolePrefix = "Team_";

        // Will be needed for tests
        internal static readonly GuildPermissions PrivilegedGuildPermissions = new GuildPermissions(
            speak: true,
            prioritySpeaker: true,
            sendMessages: true,
            kickMembers: true,
            moveMembers: true,
            muteMembers: true,
            deafenMembers: true,
            readMessageHistory: true,
            viewChannel: true);
        internal static readonly OverwritePermissions PrivilegedOverwritePermissions = new OverwritePermissions(
            speak: PermValue.Allow,
            sendMessages: PermValue.Allow,
            muteMembers: PermValue.Allow,
            deafenMembers: PermValue.Allow,
            readMessageHistory: PermValue.Allow,
            viewChannel: PermValue.Allow,
            moveMembers: PermValue.Allow);
        internal static readonly OverwritePermissions EveryonePermissions = new OverwritePermissions(
            viewChannel: PermValue.Deny,
            sendMessages: PermValue.Deny,
            readMessageHistory: PermValue.Deny);
        internal static readonly OverwritePermissions TeamPermissions = new OverwritePermissions(
            viewChannel: PermValue.Allow,
            sendMessages: PermValue.Allow,
            readMessageHistory: PermValue.Allow);

        public BotCommandHandler(ICommandContext context, GlobalTournamentsManager globalManager)
        {
            this.Context = context;
            this.GlobalManager = globalManager;
            this.Logger = Log
                .ForContext<BotCommandHandler>()
                .ForContext("guildId", this.Context.Guild.Id);
        }

        private ICommandContext Context { get; }

        private GlobalTournamentsManager GlobalManager { get; }

        private ILogger Logger { get; }

        public Task AddPlayer(IGuildUser user, string teamName)
        {
            return this.DoReadWriteActionOnCurrentTournament(
                currentTournament =>
                {
                    if (currentTournament.TryGetTeamFromName(teamName, out Team team))
                    {
                        Player player = new Player()
                        {
                            Id = user.Id,
                            Team = team
                        };
                        if (currentTournament.TryAddPlayer(player))
                        {
                            this.Logger.Debug("Player {id} successfully added to team {teamName}", user.Id, teamName);
                            return this.SendUserMessage(BotStrings.AddPlayerSuccessful(user.Mention, teamName));
                        }
                        else
                        {
                            this.Logger.Debug("Player {id} already on team; not added to {teamName}", user.Id, teamName);
                            return this.SendUserMessage(BotStrings.PlayerIsAlreadyOnTeam(user.Mention));
                        }
                    }

                    this.Logger.Debug("Player {id} could not be added to nonexistent {teamName}", user.Id, teamName);
                    return this.SendUserMessage(BotStrings.TeamDoesNotExist(teamName));
                });
        }

        public Task AddTournamentDirector(IGuildUser newDirector, string tournamentName)
        {
            if (string.IsNullOrWhiteSpace(tournamentName))
            {
                this.Logger.Debug("Did not add {id} to tournament with blank name", newDirector.Id);
                return Task.CompletedTask;
            }

            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);

            ITournamentState state = new TournamentState(this.Context.Guild.Id, tournamentName.Trim());
            bool updateSuccessful = state.TryAddDirector(newDirector.Id);

            manager.AddOrUpdateTournament(
                tournamentName,
                state,
                (name, tournamentState) =>
                {
                    updateSuccessful = tournamentState.TryAddDirector(newDirector.Id);
                    return tournamentState;
                });

            // TODO: Need to handle this differently depending on the stage. Completed shouldn't do anything, and
            // after RoleSetup we should give them the TD role.
            if (updateSuccessful)
            {
                this.Logger.Debug(
                    "Added {id} as a tournament director for {tournamentName}", newDirector.Id, tournamentName);
                return this.SendUserMessage(
                    BotStrings.AddTournamentDirectorSuccessful(tournamentName, this.Context.Guild.Name));
            }

            this.Logger.Debug(
                "{id} already a tournament director for {tournamentName}", newDirector.Id, tournamentName);
            return this.SendUserMessage(
                BotStrings.UserAlreadyTournamentDirector(tournamentName, this.Context.Guild.Name));
        }

        public Task Back()
        {
            return DoReadWriteActionOnCurrentTournament(
                async currentTournament =>
                {
                    switch (currentTournament.Stage)
                    {
                        case TournamentStage.SetRoundRobins:
                            currentTournament.TryClearReaders();
                            break;
                        case TournamentStage.AddTeams:
                            currentTournament.RoundRobinsCount = 0;
                            break;
                        case TournamentStage.AddPlayers:
                            currentTournament.TryClearTeams();
                            currentTournament.ClearSymbolsToTeam();

                            List<Task<IMessage>> getJoinTeamMessagesTasks = new List<Task<IMessage>>();
                            foreach (ulong id in currentTournament.JoinTeamMessageIds)
                            {
                                getJoinTeamMessagesTasks.Add(this.Context.Channel.GetMessageAsync(id));
                            }

                            IMessage[] joinTeamMessages = await Task.WhenAll(getJoinTeamMessagesTasks);
                            await Task.WhenAll(joinTeamMessages
                                .Select(message => this.Context.Channel.DeleteMessageAsync(message)));

                            currentTournament.ClearJoinTeamMessageIds();
                            break;
                        default:
                            // Nothing to go back to, so do nothing.
                            this.Logger.Debug("Could not go back on stage {stage}", currentTournament.Stage);
                            await this.SendUserMessage(BotStrings.CannotGoBack(currentTournament.Stage));
                            return;
                    }

                    TournamentStage previousStage = currentTournament.Stage - 1;
                    await UpdateStage(currentTournament, previousStage);
                });
        }

        public async Task ClearAll()
        {
            await CleanupAllPossibleTournamentArtifacts();
            this.Logger.Debug("All tournament artifacts cleared");
            await this.SendUserMessage(BotStrings.AllPossibleTournamentArtifactsCleaned(this.Context.Guild.Name));
        }

        public async Task End()
        {
            await DoReadWriteActionOnCurrentTournament(
                currentTournament => CleanupTournamentArtifacts(currentTournament));

            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);
            if (!manager.TryClearCurrentTournament())
            {
                this.Logger.Debug("Tournament cleanup failed");
                await this.SendUserMessage(BotStrings.TournamentWasNotRemoved);
                return;
            }

            this.Logger.Debug("Tournament cleanup finished");
            await this.SendUserMessage(BotStrings.TournamentCleanupFinished(this.Context.Guild.Name));
        }

        public async Task Finals(IGuildUser readerUser, string rawTeamNameParts)
        {
            ITextChannel channel = null;
            await DoReadWriteActionOnCurrentTournament(
                async currentTournament =>
                {
                    if (currentTournament?.Stage != TournamentStage.RunningPrelims)
                    {
                        this.Logger.Debug("Could not start finals in stage {stage}", currentTournament?.Stage);
                        await this.SendUserMessage(BotStrings.ErrorFinalsOnlySetDuringPrelims);
                        return;
                    }

                    if (!currentTournament.TryGetReader(readerUser.Id, out Reader reader))
                    {
                        this.Logger.Debug("Could not start finals because {1} is not a reader", readerUser.Id);
                        await this.SendUserMessage(BotStrings.ErrorGivenUserIsntAReader);
                        return;
                    }

                    if (rawTeamNameParts == null)
                    {
                        this.Logger.Debug(
                            "Could not start finals because no teams were specified");
                        await this.SendUserMessage(BotStrings.ErrorNoTeamsSpecified);
                        return;
                    }

                    string combinedTeamNames = string.Join(" ", rawTeamNameParts).Trim();
                    if (!TeamNameParser.TryGetTeamNamesFromParts(
                        combinedTeamNames, out IList<string> teamNames, out string errorMessage))
                    {
                        this.Logger.Debug(
                            "Could not start finals because of this error: {errorMessage}", errorMessage);
                        await this.SendUserMessage(BotStrings.ErrorGenericMessage(errorMessage));
                        return;
                    }

                    if (teamNames.Count != 2)
                    {
                        this.Logger.Debug(
                            "Could not start finals because {count} teams were specified", teamNames.Count);
                        await this.SendUserMessage(BotStrings.ErrorTwoTeamsMustBeSpecifiedFinals(teamNames.Count));
                        return;
                    }

                    Team[] teams = teamNames.Select(name => new Team()
                    {
                        Name = name
                    })
                        .ToArray();
                    if (currentTournament.Teams.Intersect(teams).Count() != teams.Length)
                    {
                        this.Logger.Debug(
                            "Could not start finals because some teams were not in the tournament", teamNames.Count);
                        await this.SendUserMessage(
                            BotStrings.ErrorAtLeastOneTeamNotInTournament(string.Join(", ", teamNames)));
                        return;
                    }

                    // Create a finals channel and give access to the teams and readers
                    Game finalsGame = new Game()
                    {
                        Reader = reader,
                        Teams = teams
                    };

                    int finalsRoundNumber = currentTournament.Schedule.Rounds.Count + 1;
                    IList<Game> finalGames =
                        currentTournament.Schedule.Rounds[currentTournament.Schedule.Rounds.Count - 1].Games;
                    int roomIndex = 0;
                    foreach (Game game in finalGames)
                    {
                        if (game.Reader.Equals(reader))
                        {
                            break;
                        }

                        roomIndex++;
                    }

                    if (roomIndex >= finalGames.Count)
                    {
                        // Need to have a fall-back somehow. For now default to the first room.
                        roomIndex = 0;
                    }

                    // TODO: We should split up this method.
                    IRole directorRole = this.Context.Guild.GetRole(currentTournament.TournamentRoles.DirectorRoleId);
                    IRole[] roomReaderRoles = currentTournament.TournamentRoles.ReaderRoomRoleIds
                        .Select(roleId => this.Context.Guild.GetRole(roleId)).ToArray();
                    Dictionary<Team, IRole> teamRoles = currentTournament.TournamentRoles.TeamRoleIds
                        .ToDictionary(kvp => kvp.Key, kvp => this.Context.Guild.GetRole(kvp.Value));

                    TournamentRoles tournamentRoles = new TournamentRoles()
                    {
                        DirectorRole = directorRole,
                        RoomReaderRoles = roomReaderRoles,
                        TeamRoles = teamRoles
                    };

                    // TODO: Look into creating the channels after the update stage so we can release the lock
                    // sooner. However, this does mean that a failure to create channels will leave us in a bad 
                    // state.
                    ICategoryChannel finalsCategoryChannel = await this.Context.Guild.CreateCategoryAsync($"Finals");
                    channel = await CreateTextChannel(
                        finalsCategoryChannel,
                        finalsGame,
                        tournamentRoles,
                        finalsRoundNumber,
                        roomIndex);
                    currentTournament.UpdateStage(
                        TournamentStage.Finals, out string nextStageTitle, out string nextStageInstructions);
                });

            if (channel != null)
            {
                this.Logger.Debug("Finals started successfully");
                await this.Context.Channel.SendMessageAsync(
                    BotStrings.FinalsParticipantsPleaseJoin(channel.Mention),
                    options: RequestOptionsSettings.Default);
            }
        }

        public Task GetCurrentTournament()
        {
            // DoReadActionOnCurrentTournament will not run the action if the tournament is null. It'll send an
            // error message to the user instead.
            return DoReadActionOnCurrentTournament(
                currentTournament => this.SendUserMessage(
                    BotStrings.CurrentTournamentInGuild(this.Context.Guild.Name, currentTournament.Name)));
        }

        public async Task GetPlayers()
        {
            IEnumerable<Tuple<Team, IEnumerable<Player>>> teamsAndPlayers = null;
            await this.DoReadActionOnCurrentTournament(
                currentTournament =>
                {
                    teamsAndPlayers = currentTournament.Teams
                        .GroupJoin(
                            currentTournament.Players,
                            team => team,
                            player => player.Team,
                            (team, players) => new Tuple<Team, IEnumerable<Player>>(team, players));
                    return Task.CompletedTask;
                });

            if (teamsAndPlayers == null)
            {
                this.Logger.Debug("Unable to get players because we could not access the current tournament");
                return;
            }
            else if (!teamsAndPlayers.Any())
            {
                this.Logger.Debug("Unable to get players because there are no teams yet");
                await this.SendUserMessage(BotStrings.NoTeamsYet);
                return;
            }

            // TODO: Look into using an embed. Embeds have 25-field limits, so use newlines in a message
            // for now to simplify the logic (no message splitting).
            string[] teamPlayerLines = await Task.WhenAll(teamsAndPlayers
                .Select(teamPlayer => this.GetTeamPlayersLine(teamPlayer)));
            string content = string.Join(Environment.NewLine, teamPlayerLines);
            await this.SendUserMessage(content);
            this.Logger.Debug("Current players returned successfully");
        }

        public Task RemovePlayer(IGuildUser user)
        {
            return this.DoReadWriteActionOnCurrentTournament(
                currentTournament =>
                {
                    if (currentTournament.TryRemovePlayer(user.Id))
                    {
                        this.Logger.Debug("Player {id} was removed from the tournament", user.Id);
                        return this.SendUserMessage(BotStrings.PlayerRemoved(user.Mention));
                    }

                    this.Logger.Debug("Player {id} wasn't on any team", user.Id);
                    return this.SendUserMessage(BotStrings.PlayerIsNotOnAnyTeam(user.Mention));
                });
        }

        public async Task RemoveTournamentDirector(IGuildUser oldDirector, string tournamentName)
        {
            if (string.IsNullOrWhiteSpace(tournamentName))
            {
                this.Logger.Debug("Couldn't remove director {id} for tournament with blank name", oldDirector.Id);
                return;
            }

            tournamentName = tournamentName.Trim();
            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);

            // TODO: Harden this. Since it's not guaranteed to be the current tournament, we can't use the helper
            // methods
            IDMChannel dmChannel = await this.Context.User.GetOrCreateDMChannelAsync();
            if (!manager.TryGetTournament(tournamentName, out ITournamentState state))
            {
                this.Logger.Debug(
                    "Couldn't remove director {id} for nonexistent tournament {tournamentName}", 
                    oldDirector.Id, 
                    tournamentName);
                await dmChannel.SendMessageAsync(
                    BotStrings.TournamentDoesNotExist(tournamentName, this.Context.Guild.Name),
                    options: RequestOptionsSettings.Default);
                return;
            }

            if (state.TryRemoveDirector(oldDirector.Id))
            {
                this.Logger.Debug(
                    "Removed {id} as a tournament director for {tournamentName}", oldDirector.Id, tournamentName);
                await dmChannel.SendMessageAsync(
                    BotStrings.RemovedTournamentDirector(tournamentName, this.Context.Guild.Name),
                    options: RequestOptionsSettings.Default);
                return;
            }

            this.Logger.Debug(
                "User {id} is not a director for {tournamentName}, so could not be removed", 
                oldDirector.Id, 
                tournamentName);
            await dmChannel.SendMessageAsync(
                BotStrings.UserNotTournamentDirector(tournamentName, this.Context.Guild.Name), options: RequestOptionsSettings.Default);
        }

        public Task Setup(string tournamentName)
        {
            // It's okay not to harden this too much, because they can retry the action, and their failure doesn't 
            // make anything inconsistent.
            if (string.IsNullOrEmpty(tournamentName))
            {
                this.Logger.Debug("Couldn't setup with blank name");
                return Task.CompletedTask;
            }

            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);

            if (!manager.TrySetCurrentTournament(tournamentName, out string errorMessage))
            {
                this.Logger.Debug("Error when setting up tournament: {errorMessage}", errorMessage);
                return this.SendUserMessage(
                    BotStrings.ErrorSettingCurrentTournament(this.Context.Guild.Name, errorMessage));
            }

            return this.DoReadWriteActionOnCurrentTournament(
                currentTournament => this.UpdateStage(currentTournament, TournamentStage.AddReaders));
        }

        public async Task Start()
        {
            bool startSucceeded = false;
            await DoReadWriteActionOnCurrentTournament(
                async currentTournament =>
                {
                    if (currentTournament?.Stage != TournamentStage.AddPlayers)
                    {
                        // !start only applies once we've started adding players
                        this.Logger.Debug("Start failed because we were in stage {stage}", currentTournament?.Stage);
                        await this.SendUserMessage(BotStrings.CommandOnlyUsedTournamentReadyStart);
                        return;
                    }

                    await UpdateStage(currentTournament, TournamentStage.BotSetup);

                    try
                    {
                        // TODO: Add more messaging around the current status
                        this.Logger.Debug("Generating tournament");
                        IScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(
                            currentTournament.RoundRobinsCount);

                        currentTournament.Schedule = scheduleFactory.Generate(
                            new HashSet<Team>(currentTournament.Teams),
                            new HashSet<Reader>(currentTournament.Readers));

                        this.Logger.Debug("Tournament generated. Creating channels and roles");
                        await this.Context.Channel.SendMessageAsync(
                            BotStrings.CreatingChannelsAndRoles, options: RequestOptionsSettings.Default);
                        await this.CreateArtifacts(currentTournament);

                        await UpdateStage(currentTournament, TournamentStage.RunningPrelims);
                        startSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        // TODO: Make the exceptions we catch more defined.
                        // Go back to the previous stage and undo any artifacts added.
                        this.Logger.Error(ex, "Error starting the tournament. Cleaning up the tournament artifacts");
                        await CleanupTournamentArtifacts(currentTournament);
                        await UpdateStage(currentTournament, TournamentStage.AddPlayers);
                        throw;
                    }
                });

            if (startSucceeded)
            {
                await this.Context.Channel.SendMessageAsync(
                    BotStrings.TournamentHasStarted(
                        MentionUtils.MentionChannel(this.Context.Channel.Id)),
                    options: RequestOptionsSettings.Default);
            }
        }

        public async Task SwitchReader(IGuildUser oldReaderUser, IGuildUser newReaderUser)
        {
            bool switchSuccessful = false;
            IRole oldReaderRole = null;
            await DoReadWriteActionOnCurrentTournament(
                async currentTournament =>
                {
                    // Only allow this after the tournament has started running
                    if (currentTournament.Stage != TournamentStage.RunningPrelims &&
                        currentTournament.Stage != TournamentStage.Finals)
                    {

                        await this.SendUserMessage(BotStrings.CommandOnlyUsedWhileTournamentRunning);
                        return;
                    }

                    // This will act unexpectedly if the user has more than one reader role.
                    oldReaderRole = this.Context.Guild.Roles
                        .Where(role => role.Name.StartsWith(ReaderRoomRolePrefix))
                        .Join(oldReaderUser.RoleIds, role => role.Id, id => id, (role, id) => role)
                        .FirstOrDefault();
                    if (oldReaderRole == null)
                    {
                        await this.SendUserMessage(BotStrings.CouldntGetRoleForTheOldReader);
                        return;
                    }

                    if (!currentTournament.IsReader(oldReaderUser.Id))
                    {
                        await this.SendUserMessage(BotStrings.NotACurrentReader(oldReaderUser.Mention));
                        return;
                    }
                    else if (currentTournament.IsReader(newReaderUser.Id))
                    {

                        await this.SendUserMessage(BotStrings.IsAlreadyReader(newReaderUser.Mention));
                        return;
                    }

                    if (!currentTournament.TryRemoveReader(oldReaderUser.Id))
                    {

                        await this.SendUserMessage(BotStrings.UnknownErrorRemovingOldReader);
                        return;
                    }

                    Reader newReader = new Reader()
                    {
                        Id = newReaderUser.Id,
                        Name = newReaderUser.Nickname ?? newReaderUser.Username
                    };
                    currentTournament.AddReaders(new Reader[] { newReader });

                    switchSuccessful = true;
                });

            if (!switchSuccessful)
            {
                return;
            }

            List<Task> roleChangeTasks = new List<Task>();
            roleChangeTasks.Add(newReaderUser.AddRoleAsync(oldReaderRole, RequestOptionsSettings.Default));
            roleChangeTasks.Add(oldReaderUser.RemoveRoleAsync(oldReaderRole, RequestOptionsSettings.Default));
            await Task.WhenAll(roleChangeTasks);
            await this.SendUserMessage(BotStrings.ReadersSwitchedSuccessfully);
        }

        private static TournamentsManager CreateTournamentsManager(ulong id)
        {
            return new TournamentsManager()
            {
                GuildId = id
            };
        }

        private static string GetTeamRoleName(Team team)
        {
            return $"{TeamRolePrefix}{team.Name}";
        }

        private static string GetRoomReaderRoleName(int roomNumber)
        {
            return $"{ReaderRoomRolePrefix}{roomNumber + 1}";
        }

        private static string GetTextRoomName(Reader reader, int roundNumber)
        {
            return $"Round_{roundNumber}_{reader.Name.Replace(" ", "_")}";
        }

        private static string GetVoiceRoomName(Reader reader)
        {
            return $"{reader.Name.Replace(" ", "_")}'s_Voice_Channel";
        }

        private async Task<Dictionary<Team, IRole>> AssignPlayerRoles(
            ITournamentState state, IDictionary<ulong, IGuildUser> users)
        {
            this.Logger.Debug("Creating team roles");
            List<Task<KeyValuePair<Team, IRole>>> addTeamRoleTasks = new List<Task<KeyValuePair<Team, IRole>>>();
            foreach (Team team in state.Teams)
            {
                addTeamRoleTasks.Add(this.CreateTeamRole(team));
            }

            IEnumerable<KeyValuePair<Team, IRole>> teamRolePairs = await Task.WhenAll(addTeamRoleTasks);
            Dictionary<Team, IRole> teamRoles = new Dictionary<Team, IRole>(teamRolePairs);
            this.Logger.Debug("Team roles created");

            this.Logger.Debug("Assigning player roles");
            List<Task> assignPlayerRoleTasks = new List<Task>();
            foreach (Player player in state.Players)
            {
                if (!teamRoles.TryGetValue(player.Team, out IRole role))
                {
                    this.Logger.Warning("Player {0} does not have a team role for team {1}", player.Id, player.Team);
                    continue;
                }

                if (!users.TryGetValue(player.Id, out IGuildUser user))
                {
                    this.Logger.Warning("Player {id} does not have a IGuildUser", player.Id);
                    continue;
                }

                assignPlayerRoleTasks.Add(user.AddRoleAsync(role, RequestOptionsSettings.Default));
            }

            await Task.WhenAll(assignPlayerRoleTasks);
            this.Logger.Debug("Finished assigning player roles");
            return teamRoles;
        }

        private async Task<IRole> AssignDirectorRole(ITournamentState state, IDictionary<ulong, IGuildUser> users)
        {
            this.Logger.Debug("Assigning director roles to {ids}", state.DirectorIds);
            IRole role = await this.Context.Guild.CreateRoleAsync(
                DirectorRoleName, permissions: PrivilegedGuildPermissions, color: Color.Gold);
            List<Task> assignRoleTasks = new List<Task>();
            foreach (ulong directorId in state.DirectorIds)
            {
                if (users.TryGetValue(directorId, out IGuildUser user))
                {
                    assignRoleTasks.Add(user.AddRoleAsync(role, RequestOptionsSettings.Default));
                }
                else
                {
                    this.Logger.Warning("Could not find director {id}", directorId);
                }
            }

            await Task.WhenAll(assignRoleTasks);
            this.Logger.Debug("Finished assigning director roles");
            return role;
        }

        private async Task<IRole[]> AssignRoomReaderRoles(ITournamentState state, IDictionary<ulong, IGuildUser> members)
        {
            this.Logger.Debug("Assigning room reader roles to the readers");
            int roomsCount = state.Teams.Count() / 2;
            Task<IRole>[] roomReaderRoleTasks = new Task<IRole>[roomsCount];
            IEnumerator<Reader> readers = state.Readers.GetEnumerator();

            for (int i = 0; i < roomsCount && readers.MoveNext(); i++)
            {
                roomReaderRoleTasks[i] = this.AssignRoomReaderRole(
                    GetRoomReaderRoleName(i), readers.Current.Id, members);
            }

            IRole[] roles = await Task.WhenAll(roomReaderRoleTasks);
            this.Logger.Debug("Finished assigning room reader roles to the readers");
            return roles;
        }

        private async Task<IRole> AssignRoomReaderRole(string roleName, ulong readerId, IDictionary<ulong, IGuildUser> members)
        {
            IRole role = await this.Context.Guild.CreateRoleAsync(
                roleName, permissions: PrivilegedGuildPermissions, color: Color.Green, options: RequestOptionsSettings.Default);
            await members[readerId].AddRoleAsync(role, options: RequestOptionsSettings.Default);
            return role;
        }

        // Removes all possible channels and roles created by the bot.
        private async Task CleanupAllPossibleTournamentArtifacts()
        {
            // Simplest way is to delete all channels that are not a main channel
            List<Task> deleteChannelTasks = new List<Task>();
            IReadOnlyCollection<IGuildChannel> channels = await this.Context.Guild.GetChannelsAsync();
            foreach (IGuildChannel channel in channels)
            {
                // This should only be accepted on the main channel.
                // TODO: This command isn't enforced on the main channel.
                if (channel != this.Context.Channel)
                {
                    deleteChannelTasks.Add(channel.DeleteAsync(RequestOptionsSettings.Default));
                }
            }

            List<Task> deleteRoleTasks = new List<Task>();
            IReadOnlyCollection<IRole> roles = this.Context.Guild.Roles;
            foreach (IRole role in roles)
            {
                string roleName = role.Name;
                if (roleName == DirectorRoleName ||
                    roleName.StartsWith(ReaderRoomRolePrefix) ||
                    roleName.StartsWith(TeamRolePrefix))
                {
                    deleteRoleTasks.Add(role.DeleteAsync(RequestOptionsSettings.Default));
                }
            }

            this.Logger.Debug("Deleting all channels other than the main channel");
            await Task.WhenAll(deleteChannelTasks);
            this.Logger.Debug("Channels deleted. Deleting all roles created by any tournament");
            await Task.WhenAll(deleteRoleTasks);
            this.Logger.Debug("Roles for any tournament deleted");
            // We don't need to update the tournament stage because this should be used when the tournament is
            // completed or no longer exists 
        }

        // Removes channels and roles.
        private async Task CleanupTournamentArtifacts(ITournamentState state)
        {
            IEnumerable<Task> deleteChannelTasks;
            if (state.ChannelIds != null)
            {
                IGuildChannel[] channels = await Task.WhenAll(state.ChannelIds
                    .Select(id => this.Context.Guild.GetChannelAsync(id, options: RequestOptionsSettings.Default)));
                deleteChannelTasks = channels
                    .Where(channel => channel != null)
                    .Select(channel => channel.DeleteAsync(options: RequestOptionsSettings.Default));
            }
            else
            {
                deleteChannelTasks = new Task[0];
            }

            IEnumerable<ulong> roleIds = state.TournamentRoles?.ReaderRoomRoleIds
                .Concat(state.TournamentRoles.TeamRoleIds.Select(kvp => kvp.Value))
                .Concat(new ulong[] { state.TournamentRoles.DirectorRoleId });
            IEnumerable<Task> deleteRoleTasks = roleIds?
                .Select(id => this.Context.Guild.GetRole(id))
                .Where(role => role != null)
                .Select(role => role.DeleteAsync(RequestOptionsSettings.Default));
            if (deleteRoleTasks == null)
            {
                deleteRoleTasks = new Task[0];
            }

            this.Logger.Debug("Deleting all channels and roles created by the tournament {name}", state.Name);
            await Task.WhenAll(deleteChannelTasks.Concat(deleteRoleTasks));
            this.Logger.Debug("All channels and roles created by the tournament {name} are deleted", state.Name);
            await UpdateStage(state, TournamentStage.Complete);
        }

        private async Task CreateArtifacts(ITournamentState state)
        {
            // GetAllMembersAsync may contain the same member multiple times, which causes ToDictionary to throw. Add
            // members manually to a dictionary instead.
            IReadOnlyCollection<IGuildUser> allMembers = await this.Context.Guild.GetUsersAsync();
            IDictionary<ulong, IGuildUser> members = new Dictionary<ulong, IGuildUser>();
            foreach (IGuildUser member in allMembers)
            {
                members[member.Id] = member;
            }

            IRole directorRole = await this.AssignDirectorRole(state, members);
            IRole[] roomReaderRoles = await this.AssignRoomReaderRoles(state, members);
            Dictionary<Team, IRole> teamRoles = await this.AssignPlayerRoles(state, members);
            TournamentRoles roles = new TournamentRoles()
            {
                DirectorRole = directorRole,
                RoomReaderRoles = roomReaderRoles,
                TeamRoles = teamRoles
            };
            state.TournamentRoles = roles.ToIds();

            // Create the voice channels
            List<Task<IVoiceChannel>> createVoiceChannelsTasks = new List<Task<IVoiceChannel>>();
            // We only need to go through the games for the first round to get all of the readers.
            Round firstRound = state.Schedule.Rounds.First();
            Debug.Assert(firstRound.Games.Select(game => game.Reader.Name).Count() ==
                firstRound.Games.Select(game => game.Reader.Name).Distinct().Count(),
                "All reader names should be unique.");
            ICategoryChannel voiceCategoryChannel = await this.Context.Guild.CreateCategoryAsync(
                "Readers", options: RequestOptionsSettings.Default);
            foreach (Game game in firstRound.Games)
            {
                createVoiceChannelsTasks.Add(this.CreateVoiceChannel(voiceCategoryChannel, roles, game.Reader));
            }

            IVoiceChannel[] voiceChannels = await Task.WhenAll(createVoiceChannelsTasks);

            // Create the text channels
            List<Task<ITextChannel>> createTextChannelsTasks = new List<Task<ITextChannel>>();
            List<ulong> textCategoryChannelIds = new List<ulong>();
            int roundNumber = 1;
            foreach (Round round in state.Schedule.Rounds)
            {
                int roomNumber = 0;
                ICategoryChannel roundCategoryChannel = await this.Context.Guild.CreateCategoryAsync(
                    $"Round {roundNumber}",
                    options: RequestOptionsSettings.Default);
                textCategoryChannelIds.Add(roundCategoryChannel.Id);

                foreach (Game game in round.Games)
                {
                    createTextChannelsTasks.Add(
                        this.CreateTextChannel(roundCategoryChannel, game, roles, roundNumber, roomNumber));
                    roomNumber++;
                }

                roundNumber++;
            }

            ITextChannel[] textChannels = await Task.WhenAll(createTextChannelsTasks);
            state.ChannelIds = voiceChannels.Select(channel => channel.Id)
                .Concat(textChannels.Select(channel => channel.Id))
                .Concat(new ulong[] { voiceCategoryChannel.Id })
                .Concat(textCategoryChannelIds)
                .ToArray();
        }

        private async Task<KeyValuePair<Team, IRole>> CreateTeamRole(Team team)
        {
            IRole role = await this.Context.Guild.CreateRoleAsync(
                GetTeamRoleName(team), color: Color.Teal, options: RequestOptionsSettings.Default);
            return new KeyValuePair<Team, IRole>(team, role);
        }

        private async Task<ITextChannel> CreateTextChannel(
            ICategoryChannel parent, Game game, TournamentRoles roles, int roundNumber, int roomNumber)
        {
            // The room and role names will be the same.
            this.Logger.Debug("Creating text channel for room {0} in round {1}", roomNumber, roundNumber);
            string name = GetTextRoomName(game.Reader, roundNumber);
            ITextChannel channel = await this.Context.Guild.CreateTextChannelAsync(
                name,
                channelProps =>
                {
                    channelProps.CategoryId = parent.Id;
                },
                RequestOptionsSettings.Default);
            this.Logger.Debug("Text channel for room {0} in round {1} created", roomNumber, roundNumber);

            this.Logger.Debug("Adding permissions to text channel for room {0} in round {1}", roomNumber, roundNumber);
            await channel.AddPermissionOverwriteAsync(
                this.Context.Guild.EveryoneRole, EveryonePermissions, RequestOptionsSettings.Default);

            // TODO: Give the bot less-than-privileged permissions.
            await channel.AddPermissionOverwriteAsync(
                this.Context.Client.CurrentUser, PrivilegedOverwritePermissions, RequestOptionsSettings.Default);
            await channel.AddPermissionOverwriteAsync(
                roles.DirectorRole, PrivilegedOverwritePermissions, RequestOptionsSettings.Default);
            await channel.AddPermissionOverwriteAsync(
                roles.RoomReaderRoles[roomNumber], PrivilegedOverwritePermissions, RequestOptionsSettings.Default);

            List<Task> addTeamRolesToChannel = new List<Task>();
            foreach (Team team in game.Teams)
            {
                if (!roles.TeamRoles.TryGetValue(team, out IRole role))
                {
                    this.Logger.Warning("Team {name} did not have a role defined.", team.Name);
                    continue;
                }

                addTeamRolesToChannel.Add(channel.AddPermissionOverwriteAsync(role, TeamPermissions, RequestOptionsSettings.Default));
            }

            await Task.WhenAll(addTeamRolesToChannel);
            this.Logger.Debug("Added permissions to text channel for room {0} in round {1}", roomNumber, roundNumber);
            return channel;
        }

        private async Task<IVoiceChannel> CreateVoiceChannel(
            ICategoryChannel parent, TournamentRoles roles, Reader reader)
        {
            this.Logger.Debug("Creating voice channel for reader {id}", reader.Id);
            string name = GetVoiceRoomName(reader);
            IVoiceChannel channel = await this.Context.Guild.CreateVoiceChannelAsync(
                name,
                channelProps =>
                {
                    channelProps.CategoryId = parent.Id;
                },
                RequestOptionsSettings.Default);
            this.Logger.Debug("Voice channel for reader {id} created", reader.Id);
            return channel;
        }

        private Task DoReadActionOnCurrentTournament(Func<IReadOnlyTournamentState, Task> action)
        {
            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);
            return manager.DoReadActionOnCurrentTournamentForMember(this.Context.User, action);
        }

        private Task DoReadWriteActionOnCurrentTournament(Func<ITournamentState, Task> action)
        {
            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);
            return manager.DoReadWriteActionOnCurrentTournamentForMember(this.Context.User, action);
        }

        private async Task<string> GetTeamPlayersLine(Tuple<Team, IEnumerable<Player>> teamPlayers)
        {
            IGuildUser[] members = await Task.WhenAll(teamPlayers.Item2
                .Select(player => this.Context.Guild.GetUserAsync(player.Id)));
            IEnumerable<string> names = members.Select(member => member.Nickname ?? member.Username);
            return $"{teamPlayers.Item1.Name}: {string.Join(", ", names)}";
        }

        private async Task SendUserMessage(string errorMessage)
        {
            IDMChannel channel = await this.Context.User.GetOrCreateDMChannelAsync();
            await channel.SendMessageAsync(errorMessage, options: RequestOptionsSettings.Default);
        }

        private async Task UpdateStage(ITournamentState state, TournamentStage stage)
        {
            state.UpdateStage(stage, out string title, out string instructions);
            if (title == null && instructions == null)
            {
                return;
            }

            EmbedBuilder embedBuilder = new EmbedBuilder();
            embedBuilder.Title = title;
            embedBuilder.Description = instructions;
            await this.Context.Channel.SendMessageAsync(embed: embedBuilder.Build(), options: RequestOptionsSettings.Default);
            this.Logger.Debug("Moved to stage {stage}", stage);
        }

        private class TournamentRoles
        {
            public IRole DirectorRole { get; set; }

            public IRole[] RoomReaderRoles { get; set; }

            public Dictionary<Team, IRole> TeamRoles { get; set; }

            public TournamentRoleIds ToIds()
            {
                return new TournamentRoleIds(
                    this.DirectorRole.Id,
                    this.RoomReaderRoles.Select(role => role.Id).ToArray(),
                    this.TeamRoles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id));
            }
        }
    }
}
