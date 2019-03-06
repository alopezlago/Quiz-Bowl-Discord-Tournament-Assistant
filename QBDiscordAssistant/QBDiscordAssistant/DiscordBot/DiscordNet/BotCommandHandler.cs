using Discord;
using Discord.Commands;
using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Game = QBDiscordAssistant.Tournament.Game;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class BotCommandHandler
    {
        private const string DirectorRoleName = "Director";
        private const string ReaderRoomRolePrefix = "Reader_Room_";
        private const string TeamRolePrefix = "Team_";
        private static readonly GuildPermissions PrivilegedGuildPermissions = new GuildPermissions(
            speak: true,
            prioritySpeaker: true,
            sendMessages: true,
            kickMembers: true,
            moveMembers: true,
            muteMembers: true,
            deafenMembers: true,
            readMessageHistory: true,
            viewChannel: true);
        private static readonly OverwritePermissions PrivilegedOverwritePermissions = new OverwritePermissions(
            speak: PermValue.Allow,
            sendMessages: PermValue.Allow,
            muteMembers: PermValue.Allow,
            deafenMembers: PermValue.Allow,
            readMessageHistory: PermValue.Allow,
            viewChannel: PermValue.Allow,
            moveMembers: PermValue.Allow);
        private static readonly OverwritePermissions EveryonePermissions = new OverwritePermissions(
            viewChannel: PermValue.Deny,
            sendMessages: PermValue.Deny,
            readMessageHistory: PermValue.Deny);
        private static readonly OverwritePermissions TeamPermissions = new OverwritePermissions(
            viewChannel: PermValue.Allow,
            sendMessages: PermValue.Allow,
            readMessageHistory: PermValue.Allow);

        public BotCommandHandler(ICommandContext context, GlobalTournamentsManager globalManager)
        {
            this.Context = context;
            this.GlobalManager = globalManager;
        }

        private ICommandContext Context { get; }

        private GlobalTournamentsManager GlobalManager { get; }

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
                            return this.SendUserMessage(
                                string.Format(BotStrings.AddPlayerSuccessful, user.Mention, teamName));
                        }
                        else
                        {
                            return this.SendUserMessage(
                                string.Format(BotStrings.PlayerIsAlreadyOnTeam, user.Mention));
                        }
                    }


                    return this.SendUserMessage(string.Format(BotStrings.TeamDoesNotExist, teamName));
                });
        }

        public Task AddTournamentDirector(IGuildUser newDirector, string tournamentName)
        {
            if (string.IsNullOrWhiteSpace(tournamentName))
            {
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
                return this.SendUserMessage(
                    string.Format(BotStrings.AddTournamentDirectorSuccessful, tournamentName, this.Context.Guild.Name));
            }

            return this.SendUserMessage(
                string.Format(BotStrings.UserAlreadyTournamentDirector, tournamentName, this.Context.Guild.Name));
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
                            await this.SendUserMessage(
                                string.Format(BotStrings.CannotGoBack, currentTournament.Stage));
                            return;
                    }

                    TournamentStage previousStage = currentTournament.Stage - 1;
                    await UpdateStage(currentTournament, previousStage);
                });
        }

        public async Task ClearAll()
        {
            await CleanupAllPossibleTournamentArtifacts();
            await this.SendUserMessage(
                string.Format(BotStrings.AllPossibleTournamentArtifactsCleaned, this.Context.Guild.Name));
        }

        public async Task End()
        {
            await DoReadWriteActionOnCurrentTournament(
                currentTournament => CleanupTournamentArtifacts(currentTournament));

            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);
            if (!manager.TryClearCurrentTournament())
            {
                await this.SendUserMessage(BotStrings.TournamentWasNotRemoved);
                return;
            }

            await this.SendUserMessage(string.Format(BotStrings.TournamentCleanupFinished, this.Context.Guild.Name));
        }

        public async Task Finals(IGuildUser readerUser, string rawTeamNameParts)
        {
            ITextChannel channel = null;
            await DoReadWriteActionOnCurrentTournament(
                async currentTournament =>
                {
                    if (currentTournament?.Stage != TournamentStage.RunningPrelims)
                    {
                        await this.SendUserMessage(BotStrings.ErrorFinalsOnlySetDuringPrelims);
                        return;
                    }

                    if (!currentTournament.TryGetReader(readerUser.Id, out Reader reader))
                    {
                        await this.SendUserMessage(BotStrings.ErrorGivenUserIsntAReader);
                        return;
                    }

                    if (rawTeamNameParts == null)
                    {
                        await this.SendUserMessage(BotStrings.ErrorNoTeamsSpecified);
                        return;
                    }

                    string combinedTeamNames = string.Join(" ", rawTeamNameParts).Trim();
                    if (!TeamNameParser.TryGetTeamNamesFromParts(
                        combinedTeamNames, out HashSet<string> teamNames, out string errorMessage))
                    {
                        await this.SendUserMessage(string.Format(BotStrings.ErrorGenericMessage, errorMessage));
                        return;
                    }

                    if (teamNames.Count != 2)
                    {
                        await this.SendUserMessage(
                            string.Format(BotStrings.ErrorTwoTeamsMustBeSpecifiedFinals, teamNames.Count));
                        return;
                    }

                    Team[] teams = teamNames.Select(name => new Team()
                    {
                        Name = name
                    })
                        .ToArray();
                    if (currentTournament.Teams.Intersect(teams).Count() != teams.Length)
                    {
                        // TODO: Improve error message by explicitly searching for the missing team.
                        await this.SendUserMessage(
                            string.Format(BotStrings.ErrorAtLeastOneTeamNotInTournament, string.Join(",", teamNames)));
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
                    // stage.
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
                await this.Context.Channel.SendMessageAsync(
                    string.Format(BotStrings.FinalsParticipantsPleaseJoin, channel.Mention));
            }
        }

        public Task GetCurrentTournament()
        {
            // DoReadActionOnCurrentTournament will not run the action if the tournament is null. It'll send an
            // error message to the user instead.
            return DoReadActionOnCurrentTournament(
                currentTournament => this.SendUserMessage(
                    string.Format(BotStrings.CurrentTournamentInGuild, this.Context.Guild.Name, currentTournament.Name)));
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
                return;
            }
            else if (!teamsAndPlayers.Any())
            {
                await this.SendUserMessage(BotStrings.NoTeamsYet);
                return;
            }

            // TODO: Look into using an embed. Embeds have 25-field limits, so use newlines in a message
            // for now to simplify the logic (no message splitting).
            string[] teamPlayerLines = await Task.WhenAll(teamsAndPlayers
                .Select(teamPlayer => this.GetTeamPlayersLine(teamPlayer)));
            string content = string.Join(Environment.NewLine, teamPlayerLines);
            await this.SendUserMessage(content);
        }

        public Task RemovePlayer(IGuildUser user)
        {
            return this.DoReadWriteActionOnCurrentTournament(
                currentTournament =>
                {
                    if (currentTournament.TryRemovePlayer(user.Id))
                    {
                        return this.SendUserMessage(string.Format(BotStrings.PlayerRemoved, user.Mention));
                    }

                    return this.SendUserMessage(string.Format(BotStrings.PlayerIsNotOnAnyTeam, user.Mention));
                });
        }

        public async Task RemoveTournamentDirector(IGuildUser oldDirector, string tournamentName)
        {
            if (string.IsNullOrWhiteSpace(tournamentName))
            {
                return;
            }

            tournamentName = tournamentName.Trim();
            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);

            // TODO: Harden this. Since it's not guaranteed to be the current tournament, we can't use the helper
            // methods
            IDMChannel dmChannel = await this.Context.User.GetOrCreateDMChannelAsync();
            if (!manager.TryGetTournament(tournamentName, out ITournamentState state))
            {
                await dmChannel.SendMessageAsync(
                    string.Format(BotStrings.TournamentDoesNotExist, tournamentName, this.Context.Guild.Name));
                return;
            }

            if (state.TryRemoveDirector(oldDirector.Id))
            {
                await dmChannel.SendMessageAsync(
                    string.Format(BotStrings.RemovedTournamentDirector, tournamentName, this.Context.Guild.Name));
                return;
            }

            await dmChannel.SendMessageAsync(
                string.Format(BotStrings.UserNotTournamentDirector, tournamentName, this.Context.Guild.Name));
        }

        public Task Setup(string tournamentName)
        {
            // It's okay not to harden this too much, because they can retry the action, and their failure doesn't 
            // make anything inconsistent.
            if (string.IsNullOrEmpty(tournamentName))
            {
                return Task.CompletedTask;
            }

            TournamentsManager manager = this.GlobalManager.GetOrAdd(this.Context.Guild.Id, CreateTournamentsManager);

            if (!manager.TrySetCurrentTournament(tournamentName, out string errorMessage))
            {                
                return this.SendUserMessage(
                    string.Format(BotStrings.ErrorSettingCurrentTournament, this.Context.Guild.Name, errorMessage));
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
                        return;
                    }

                    await UpdateStage(currentTournament, TournamentStage.BotSetup);

                    try
                    {
                        // TODO: Add more messaging around the current status
                        IScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(
                            currentTournament.RoundRobinsCount);
                        currentTournament.Schedule = scheduleFactory.Generate(
                            new HashSet<Team>(currentTournament.Teams),
                            new HashSet<Reader>(currentTournament.Readers));

                        await this.Context.Channel.SendMessageAsync(BotStrings.CreatingChannelsAndRoles);
                        await this.CreateArtifacts(currentTournament);

                        await UpdateStage(currentTournament, TournamentStage.RunningPrelims);
                        startSucceeded = true;
                    }
                    catch (Exception)
                    {
                        // TODO: Make the exceptions we catch more-defined.
                        // Go back to the previous stage and undo any artifacts added.
                        await CleanupTournamentArtifacts(currentTournament);
                        await UpdateStage(currentTournament, TournamentStage.AddPlayers);
                        throw;
                    }
                });

            if (startSucceeded)
            {

                await this.Context.Channel.SendMessageAsync(string.Format(
                    BotStrings.TournamentHasStarted, MentionUtils.MentionChannel(this.Context.Channel.Id)));
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

                        await this.SendUserMessage(BotStrings.ThisCommandCanOnlyBeUsedWhileTournamentRunning);
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
                        await this.SendUserMessage(string.Format(BotStrings.NotACurrentReader, oldReaderUser.Mention));
                        return;
                    }
                    else if (currentTournament.IsReader(newReaderUser.Id))
                    {
                        
                        await this.SendUserMessage(string.Format(BotStrings.IsAlreadyReader, newReaderUser.Mention));
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
            roleChangeTasks.Add(newReaderUser.AddRoleAsync(oldReaderRole));
            roleChangeTasks.Add(oldReaderUser.RemoveRoleAsync(oldReaderRole));
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
            return $"{reader.Name}'s_Voice_Channel";
        }

        private async Task<Dictionary<Team, IRole>> AssignPlayerRoles(
            ITournamentState state, IDictionary<ulong, IGuildUser> users)
        {
            List<Task<KeyValuePair<Team, IRole>>> addTeamRoleTasks = new List<Task<KeyValuePair<Team, IRole>>>();
            foreach (Team team in state.Teams)
            {
                addTeamRoleTasks.Add(this.CreateTeamRole(team));
            }

            IEnumerable<KeyValuePair<Team, IRole>> teamRolePairs = await Task.WhenAll(addTeamRoleTasks);
            Dictionary<Team, IRole> teamRoles = new Dictionary<Team, IRole>(teamRolePairs);

            List<Task> assignPlayerRoleTasks = new List<Task>();
            foreach (Player player in state.Players)
            {
                if (!teamRoles.TryGetValue(player.Team, out IRole role))
                {
                    Console.Error.WriteLine($"Player {player.Id} does not have a team role for team {player.Team}.");
                    continue;
                }

                if (!users.TryGetValue(player.Id, out IGuildUser user))
                {
                    Console.Error.WriteLine($"Player {player.Id} does not have a IGuildUser.");
                    continue;
                }

                assignPlayerRoleTasks.Add(user.AddRoleAsync(role));
            }

            await Task.WhenAll(assignPlayerRoleTasks);
            return teamRoles;
        }

        private async Task<IRole> AssignDirectorRole(ITournamentState state, IDictionary<ulong, IGuildUser> users)
        {
            IRole role = await this.Context.Guild.CreateRoleAsync(
                DirectorRoleName, permissions: PrivilegedGuildPermissions, color: Color.Gold);
            List<Task> assignRoleTasks = new List<Task>();
            foreach (ulong directorId in state.DirectorIds)
            {
                if (users.TryGetValue(directorId, out IGuildUser user))
                {
                    assignRoleTasks.Add(user.AddRoleAsync(role));
                }
                else
                {
                    Console.Error.WriteLine($"Could not find director with ID {directorId}");
                }
            }

            await Task.WhenAll(assignRoleTasks);
            return role;
        }

        private Task<IRole[]> AssignRoomReaderRoles(ITournamentState state, IDictionary<ulong, IGuildUser> members)
        {
            int roomsCount = state.Teams.Count() / 2;
            Task<IRole>[] roomReaderRoleTasks = new Task<IRole>[roomsCount];
            IEnumerator<Reader> readers = state.Readers.GetEnumerator();

            for (int i = 0; i < roomsCount && readers.MoveNext(); i++)
            {
                roomReaderRoleTasks[i] = this.AssignRoomReaderRole(
                    GetRoomReaderRoleName(i), readers.Current.Id, members);
            }

            return Task.WhenAll(roomReaderRoleTasks);
        }

        private async Task<IRole> AssignRoomReaderRole(string roleName, ulong readerId, IDictionary<ulong, IGuildUser> members)
        {
            IRole role = await this.Context.Guild.CreateRoleAsync(
                roleName, permissions: PrivilegedGuildPermissions, color: Color.Green);
            await members[readerId].AddRoleAsync(role);
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
                    deleteChannelTasks.Add(channel.DeleteAsync());
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
                    deleteRoleTasks.Add(role.DeleteAsync());
                }
            }

            await Task.WhenAll(deleteChannelTasks);
            await Task.WhenAll(deleteRoleTasks);
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
                    .Select(id => this.Context.Guild.GetChannelAsync(id)));
                deleteChannelTasks = channels.Select(channel => channel.DeleteAsync());
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
                .Select(role => role.DeleteAsync());
            if (deleteRoleTasks == null)
            {
                deleteRoleTasks = new Task[0];
            }

            await Task.WhenAll(deleteChannelTasks.Concat(deleteRoleTasks));
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
            ICategoryChannel voiceCategoryChannel = await this.Context.Guild.CreateCategoryAsync("Readers");
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
                    $"Round {roundNumber}");
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
            IRole role = await this.Context.Guild.CreateRoleAsync(GetTeamRoleName(team), color: Color.Teal);
            return new KeyValuePair<Team, IRole>(team, role);
        }

        private async Task<ITextChannel> CreateTextChannel(
            ICategoryChannel parent, Game game, TournamentRoles roles, int roundNumber, int roomNumber)
        {
            // The room and role names will be the same.
            string name = GetTextRoomName(game.Reader, roundNumber);
            ITextChannel channel = await this.Context.Guild.CreateTextChannelAsync(
                name,
                channelProps =>
                {
                    channelProps.CategoryId = parent.Id;
                });
            
            await channel.AddPermissionOverwriteAsync(this.Context.Guild.EveryoneRole, EveryonePermissions);

            // TODO: Give the bot less-than-privileged permissions.
            await channel.AddPermissionOverwriteAsync(this.Context.Client.CurrentUser, PrivilegedOverwritePermissions);
            await channel.AddPermissionOverwriteAsync(roles.DirectorRole, PrivilegedOverwritePermissions);
            await channel.AddPermissionOverwriteAsync(roles.RoomReaderRoles[roomNumber], PrivilegedOverwritePermissions);

            List<Task> addTeamRolesToChannel = new List<Task>();
            foreach (Team team in game.Teams)
            {
                if (!roles.TeamRoles.TryGetValue(team, out IRole role))
                {
                    Console.Error.WriteLine($"Team {team.Name} did not have a role defined.");
                    continue;
                }

                addTeamRolesToChannel.Add(channel.AddPermissionOverwriteAsync(role, TeamPermissions));
            }

            await Task.WhenAll(addTeamRolesToChannel);
            return channel;
        }

        private async Task<IVoiceChannel> CreateVoiceChannel(
            ICategoryChannel parent, TournamentRoles roles, Reader reader)
        {
            string name = GetVoiceRoomName(reader);
            // TODO: Verify this creates the channel under the category
            IVoiceChannel channel = await this.Context.Guild.CreateVoiceChannelAsync(
                name,
                channelProps =>
                {
                    channelProps.CategoryId = parent.Id;
                });
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
            await channel.SendMessageAsync(errorMessage);
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
            await this.Context.Channel.SendMessageAsync(embed: embedBuilder.Build());
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
