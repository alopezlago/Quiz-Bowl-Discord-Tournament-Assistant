using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace QBDiscordAssistant.Discord
{
    public class BotCommands
    {
        private const string DirectorRoleName = "Director";
        private const string ReaderRoomRolePrefix = "Reader_Room_";
        private const string TeamRolePrefix = "Team_";
        private const Permissions PrioritySpeaker = (Permissions)256;
        private const Permissions PrivilegedPermissions = Permissions.UseVoiceDetection |
            Permissions.UseVoice |
            Permissions.Speak |
            Permissions.SendMessages |
            Permissions.KickMembers |
            Permissions.MuteMembers |
            Permissions.DeafenMembers |
            Permissions.ReadMessageHistory |
            Permissions.AccessChannels |
            PrioritySpeaker;

        // TODO: Move all of the implementation to the BotCommandHandler methods so that they can be unit tested.
        // TODO: Instead of sending confirmations to the channel, maybe send then to the user who did them? One issue is
        // that they're not going to be looking at their DMs.
        // TODO: Look into abstracting away some of the "get members of X". Should simplify the permissions setting.
        // TODO: Set permissions per team and per reader. Should reduce the number of roles we need to create.

        // TODO: strings are split by spaces, so we need to get the parameter from the raw string.
        [Command("addTD")]
        [Description("Adds a tournament director to a tournament, and creates that tournament if it doesn't exist yet.")]
        [RequireOwner]
        [RequirePermissions(Permissions.Administrator)]
        public Task AddTournamentDirector(
            CommandContext context,
            [Description("Member to add as the tournament director (as a @mention).")] DiscordMember newDirector,
            [Description("Name of the tournament.")] params string[] tournamentNameParts)
        {
            if (IsMainChannel(context) && tournamentNameParts.Length > 0)
            {
                string tournamentName = string.Join(" ", tournamentNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                ITournamentState state = new TournamentState(context.Guild.Id, tournamentName);
                bool updateSuccessful = state.TryAddDirector(newDirector.Id);

                manager.AddOrUpdateTournament(
                    tournamentName,
                    new TournamentState(context.Guild.Id, tournamentName), (name, tournamentState) =>
                    {
                        updateSuccessful = tournamentState.TryAddDirector(newDirector.Id);
                        return tournamentState;
                    });

                // TODO: Need to handle this differently depending on the stage. Completed shouldn't do anything, and
                // after RoleSetup we should give them the TD role.
                if (updateSuccessful)
                {
                    return context.Member.SendMessageAsync($"Added tournament director to tournament '{tournamentName}'.");
                }
                else
                {
                    return context.Member.SendMessageAsync($"User is already a director of '{tournamentName}'.");
                }
            }

            return Task.CompletedTask;
        }

        [Command("removeTD")]
        [Description("Removes a tournament director from a tournament.")]
        [RequireOwner]
        [RequirePermissions(Permissions.Administrator)]
        public Task RemoveTournamentDirector(
            CommandContext context,
            [Description("Member to add as the tournament director (as a @mention).")] DiscordMember newDirector,
            [Description("Name of the tournament.")] params string[] tournamentNameParts)
        {
            if (IsMainChannel(context))
            {
                string tournamentName = string.Join(" ", tournamentNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();

                // TODO: Harden this. Since it's not guaranteed to be the current tournament, we can't use the helper
                // methods
                if (manager.TryGetTournament(tournamentName, out ITournamentState state))
                {
                    if (state.TryRemoveDirector(newDirector.Id))
                    {
                        return context.Channel.SendMessageAsync(
                            $"Removed tournament director from tournament '{tournamentName}'.");
                    }
                    else
                    {
                        return context.Channel.SendMessageAsync(
                           $"User is not a director for tournament '{tournamentName}', or user was just removed.");
                    }
                }
            }

            return Task.CompletedTask;
        }

        [Command("getCurrentTournament")]
        [Description("Gets the name of the current tournament, if it exists.")]
        public Task GetCurrentTournament(CommandContext context)
        {
            if (IsMainChannel(context))
            {
                // DoReadActionOnCurrentTournament will not run the action if the tournament is null. It'll send an
                // error message to the user instead.
                return DoReadActionOnCurrentTournament(context,
                    currentTournament =>
                        context.Channel.SendMessageAsync($"Current tournament: {currentTournament.Name}"));
            }

            return Task.CompletedTask;
        }

        [Command("setup")]
        [Description("Begins the setup phase of the tournament, where readers and teams can be added.")]
        public Task Setup(
            CommandContext context,
            [Description("Name of the tournament.")] params string[] rawTournamentNameParts)
        {
            if (!IsMainChannel(context))
            {
                return Task.CompletedTask;
            }

            // It's okay not to harden this too much, because they can retry the action, and their failure doesn't 
            // make anything inconsistent.
            string tournamentName = string.Join(" ", rawTournamentNameParts).Trim();
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            bool hasSetupPermissions = IsAdminUser(context, context.Member) ||
                (manager.TryGetTournament(tournamentName, out ITournamentState state) && state.IsDirector(context.User.Id));
            if (!hasSetupPermissions)
            {
                return context.Member.SendMessageAsync("You do not have permissions to setup that tournament.");
            }

            if (!manager.TrySetCurrentTournament(tournamentName))
            {
                return context.Member.SendMessageAsync(
                    "That tournament can't be set up right now. Another tournament may be currently running. If one isn't being run, try the command again.");
            }

            return DoReadWriteActionOnCurrentTournament(context,
                currentTournament => UpdateStage(context.Channel, currentTournament, TournamentStage.AddReaders));
        }

        // TODO: Add removeTeams

        // TODO: Add clear (reset players, teams, readers, round robins)

        [Command("addPlayer")]
        [Description("Adds a player to a team.")]
        public Task AddPlayer(
            CommandContext context,
            [Description("Member to add as the player (as a @mention).")] DiscordMember member,
            [Description("Team name.")] params string[] rawTeamNameParts)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                string teamName = string.Join(" ", rawTeamNameParts).Trim();
                return DoReadWriteActionOnCurrentTournament(
                    context,
                    currentTournament =>
                    {
                        if (currentTournament.TryGetTeamFromName(teamName, out Team team))
                        {
                            Player player = new Player()
                            {
                                Id = member.Id,
                                Team = team
                            };
                            if (currentTournament.TryAddPlayer(player))
                            {
                                return context.Member.SendMessageAsync($"Player {member.Mention} added to team '{teamName}'.");
                            }
                            else
                            {
                                return context.Member.SendMessageAsync($"Player {member.Mention} is already on a team.");
                            }
                        }
                        else
                        {
                            return context.Member.SendMessageAsync($"Team '{teamName}' does not exist.");
                        }
                    });
            }

            return Task.CompletedTask;
        }

        [Command("removePlayer")]
        [Description("Removes a player from a team.")]
        public Task RemovePlayer(
            CommandContext context,
            [Description("Member to add as the player (as a @mention).")] DiscordMember member)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                return DoReadWriteActionOnCurrentTournament(
                    context,
                    currentTournament =>
                    {
                        if (currentTournament.TryRemovePlayer(member.Id))
                        {
                            return context.Member.SendMessageAsync($"Player {member.Mention} removed.");
                        }
                        else
                        {
                            return context.Member.SendMessageAsync($"Player {member.Mention} is not on any team.");
                        }
                    });
            }

            return Task.CompletedTask;
        }

        [Command("getPlayers")]
        [Description("Gets the players in the current tournament, grouped by their team.")]
        public async Task GetPlayers(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                IEnumerable<Tuple<Team, IEnumerable<Player>>> teamsAndPlayers = null;
                await DoReadActionOnCurrentTournament(
                    context,
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

                if (teamsAndPlayers != null)
                {
                    // TODO: Look into using an embed. Embeds have 25-field limits, so use newlines in a message
                    // for now to simplify the logic (no message splitting).
                    string[] teamPlayerLines = await Task.WhenAll(teamsAndPlayers
                        .Select(teamPlayer => GetTeamPlayersLine(context, teamPlayer)));
                    string content = string.Join(Environment.NewLine, teamPlayerLines);
                    await context.Member.SendMessageAsync(content);
                }
            }
        }

        [Command("start")]
        [Description("Starts the current tournament")]
        public async Task Start(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                bool startSucceeded = false;
                await DoReadWriteActionOnCurrentTournament(
                    context,
                    async currentTournament =>
                    {
                        if (currentTournament?.Stage != TournamentStage.AddPlayers)
                        {
                            // !start only applies once we've started adding players
                            return;
                        }

                        await UpdateStage(context.Channel, currentTournament, TournamentStage.BotSetup);

                        // TODO: Add more messaging around the current status
                        IScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(
                            currentTournament.RoundRobinsCount);
                        currentTournament.Schedule = scheduleFactory.Generate(
                            new HashSet<Team>(currentTournament.Teams),
                            new HashSet<Reader>(currentTournament.Readers));

                        await context.Channel.SendMessageAsync("Creating the channels and roles...");
                        await CreateArtifacts(context, currentTournament);

                        await UpdateStage(context.Channel, currentTournament, TournamentStage.RunningPrelims);
                        startSucceeded = true;
                    });

                if (startSucceeded)
                {
                    await context.Channel.SendMessageAsync($"{context.Channel.Mention}: tournament has started.");
                }
            }
        }

        [Command("back")]
        [Description("Undoes the current stage and returns to the previous stage.")]
        public Task Back(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                return DoReadWriteActionOnCurrentTournament(
                    context,
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

                                List<Task<DiscordMessage>> getJoinTeamMessagesTasks = new List<Task<DiscordMessage>>();
                                foreach (ulong id in currentTournament.JoinTeamMessageIds)
                                {
                                    getJoinTeamMessagesTasks.Add(context.Channel.GetMessageAsync(id));
                                }

                                DiscordMessage[] joinTeamMessages = await Task.WhenAll(getJoinTeamMessagesTasks);
                                await context.Channel.DeleteMessagesAsync(
                                    joinTeamMessages, "Deleting join team messages since we are redoing the add teams stage.");

                                currentTournament.ClearJoinTeamMessageIds();
                                break;
                            default:
                                // Nothing to go back to, so do nothing.
                                await context.Member.SendMessageAsync(
                                    $"Cannot go back from the stage {currentTournament.Stage.ToString()}.");
                                return;
                        }

                        TournamentStage previousStage = currentTournament.Stage - 1;
                        await UpdateStage(context.Channel, currentTournament, previousStage);
                    });
            }

            return Task.CompletedTask;
        }

        [Command("switchreaders")]
        [Description("Switches the two readers.")]
        public Task SwitchReader(
            CommandContext context,
            [Description("Old reader to replace (as a @mention).")] DiscordMember oldReaderMember,
            [Description("New reader (as a @mention).")] DiscordMember newReaderMember)
        {
            // TODO: Consider rewriting methods to exit early if we're not in the main channel/have TD privileges.
            // Alternatively, consider writing an Attribute that does this check for us.
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                return DoReadWriteActionOnCurrentTournament(
                    context,
                    async currentTournament =>
                    {
                        // Only allow this after the tournament has started running
                        if (currentTournament.Stage != TournamentStage.RunningPrelims &&
                            currentTournament.Stage != TournamentStage.Finals)
                        {
                            await context.Member.SendMessageAsync("This command can only be used while the tournament is running. Use !back if you are still setting up the tournament.");
                            return;
                        }

                        DiscordRole oldReaderRole = oldReaderMember.Roles
                            .FirstOrDefault(role => role.Name.StartsWith(ReaderRoomRolePrefix));
                        if (oldReaderRole == null)
                        {
                            await context.Member.SendMessageAsync("Couldn't get the role for the old reader. Readers were not switched. You may need to manually switch the roles.");
                            return;
                        }

                        if (!currentTournament.IsReader(oldReaderMember.Id))
                        {
                            await context.Member.SendMessageAsync($"{oldReaderMember.Mention} is not a current reader. You can only replace existing readers.");
                            return;
                        }
                        else if (currentTournament.IsReader(newReaderMember.Id))
                        {
                            await context.Member.SendMessageAsync($"{newReaderMember.Mention} is already a reader. The new reader must not be an existing reader.");
                            return;
                        }

                        if (!currentTournament.TryRemoveReader(oldReaderMember.Id))
                        {
                            await context.Member.SendMessageAsync("Unknown error when trying to remove the old reader.");
                            return;
                        }

                        Reader newReader = new Reader()
                        {
                            Id = newReaderMember.Id,
                            Name = newReaderMember.Nickname ?? newReaderMember.DisplayName
                        };
                        currentTournament.AddReaders(new Reader[] { newReader });

                        List<Task> roleChangeTasks = new List<Task>();
                        roleChangeTasks.Add(newReaderMember.GrantRoleAsync(oldReaderRole, "Adding reader role to new reader."));
                        roleChangeTasks.Add(oldReaderMember.RevokeRoleAsync(oldReaderRole, "Removing reader role from oldreader."));
                        await Task.WhenAll(roleChangeTasks);
                        await context.Member.SendMessageAsync("Readers switched successfully.");

                        // Only allow this after the tournament has started running
                        if (currentTournament.Stage != TournamentStage.RunningPrelims &&
                            currentTournament.Stage != TournamentStage.Finals)
                        {
                            await context.Member.SendMessageAsync("This command can only be used while the tournament is running. Use !back if you are still setting up the tournament.");
                        }
                    });
            }

            return Task.CompletedTask;
        }

        [Command("finals")]
        [Description("Sets up a room for the finals participants and reader.")]
        public async Task Finals(
            CommandContext context,
            [Description("Reader for the finals (as a @mention).")] DiscordMember readerMember,
            [Description("Name of the two teams in the finals, separated by a comma.")] params string[] rawTeamNameParts)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                DiscordChannel channel = null;
                await DoReadWriteActionOnCurrentTournament(
                    context,
                    async currentTournament =>
                    {
                        if (currentTournament?.Stage != TournamentStage.RunningPrelims)
                        {
                            await context.Member.SendMessageAsync(
                                "Error: finals can only be set during the prelims.");
                            return;
                        }

                        if (!currentTournament.TryGetReader(readerMember.Id, out Reader reader))
                        {
                            await context.Member.SendMessageAsync(
                                "Error: given user isn't a reader.");
                            return;
                        }

                        if (rawTeamNameParts == null)
                        {
                            await context.Member.SendMessageAsync(
                                "Error: No teams specified.");
                            return;
                        }

                        string combinedTeamNames = string.Join(" ", rawTeamNameParts).Trim();
                        if (!TeamNameParser.TryGetTeamNamesFromParts(
                            combinedTeamNames, out HashSet<string> teamNames, out string errorMessage))
                        {
                            await context.Member.SendMessageAsync($"Error: {errorMessage}");
                            return;
                        }

                        if (teamNames.Count != 2)
                        {
                            await context.Member.SendMessageAsync(
                                $"Error: two teams must be specified in the finals. You have specified {teamNames.Count}.");
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
                            await context.Member.SendMessageAsync(
                                $"Error: At least one team specified is not in the tournament. Teams specified: {string.Join(",", teamNames)}");
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
                        DiscordRole directorRole = context.Guild.GetRole(currentTournament.TournamentRoles.DirectorRoleId);
                        DiscordRole[] roomReaderRoles = currentTournament.TournamentRoles.ReaderRoomRoleIds
                            .Select(roleId => context.Guild.GetRole(roleId)).ToArray();
                        Dictionary<Team, DiscordRole> teamRoles = currentTournament.TournamentRoles.TeamRoleIds
                            .ToDictionary(kvp => kvp.Key, kvp => context.Guild.GetRole(kvp.Value));

                        TournamentRoles tournamentRoles = new TournamentRoles()
                        {
                            DirectorRole = directorRole,
                            RoomReaderRoles = roomReaderRoles,
                            TeamRoles = teamRoles
                        };

                        // TODO: Look into creating the channels after the update stage so we can release the lock
                        // sooner. However, this does mean that a failure to create channels will leave us in a bad 
                        // stage.
                        DiscordChannel finalsCategoryChannel = await context.Guild.CreateChannelAsync(
                            $"Finals", ChannelType.Category);
                        channel = await CreateTextChannel(
                            context,
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
                    await context.Channel.SendMessageAsync(
                        $"Finals participants: please join the room {channel.Name} and join the voice channel for that room number.");
                }
            }
        }

        [Command("end")]
        [Description("Ends the current tournament.")]
        public async Task End(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                await DoReadWriteActionOnCurrentTournament(
                    context,
                    currentTournament => CleanupTournamentArtifacts(context, currentTournament));

                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.TryClearCurrentTournament())
                {
                    await context.Member.SendMessageAsync("Tournament cleanup finished.");
                }
                else
                {
                    await context.Member.SendMessageAsync("Tournament was not removed from the list of pending tournaments. Try the command again.");
                }
            }
        }

        // TODO: Implement !back, which will go back a stage.
        // This does mean that, if we go back to add players, we'll need to re-send the messages, and possibly clear
        // all the players so that the reactions are consistent.

        // Commands:
        // Note that Admins can perform all TD actions.
        // We may want to guide TD through commands. So tell them after !addTeams, "now add users", after !roundrobins, "!start", etc.
        // 
        // Only in the main room:
        // !addTD @user <tournament name> [Admin]
        // !removeTD @user <tournament name> [Admin]
        // !getCurrentTournament [Any]
        // !setup [TD]
        // !addPlayer @user <team name> [TD]
        // !removePlayer @user [TD]
        // !nofinal [TD] (TODO later, sets no final, when we move to advantaged final by default)
        // !start [TD]
        // !end [TD, Admin]
        //
        // !win <team name> [reader] (TODO later, do this only if we have time.)

        private static async Task CreateArtifacts(CommandContext context, ITournamentState state)
        {
            // GetAllMembersAsync may contain the same member multiple times, which causes ToDictionary to throw. Add
            // members manually to a dictionary instead.
            IReadOnlyList<DiscordMember> allMembers = await context.Guild.GetAllMembersAsync();
            IDictionary<ulong, DiscordMember> members = new Dictionary<ulong, DiscordMember>();
            foreach (DiscordMember member in allMembers)
            {
                members[member.Id] = member;
            }

            DiscordRole directorRole = await AssignDirectorRole(context, state, members);
            DiscordRole[] roomReaderRoles = await AssignRoomReaderRoles(context, state, members);
            Dictionary<Team, DiscordRole> teamRoles = await AssignPlayerRoles(context, state, members);
            TournamentRoles roles = new TournamentRoles()
            {
                DirectorRole = directorRole,
                RoomReaderRoles = roomReaderRoles,
                TeamRoles = teamRoles
            };
            state.TournamentRoles = roles.ToIds();

            // Create the voice channels
            List<Task<DiscordChannel>> createVoiceChannelsTasks = new List<Task<DiscordChannel>>();
            // We only need to go through the games for the first round to get all of the readers.
            Round firstRound = state.Schedule.Rounds.First();
            Debug.Assert(firstRound.Games.Select(game => game.Reader.Name).Count() ==
                firstRound.Games.Select(game => game.Reader.Name).Distinct().Count(),
                "All reader names should be unique.");
            foreach (Game game in firstRound.Games)
            {
                createVoiceChannelsTasks.Add(CreateVoiceChannel(context, roles, game.Reader));
            }

            await Task.WhenAll(createVoiceChannelsTasks);

            // Create the text channels
            List<Task> createTextChannelsTasks = new List<Task>();
            int roundNumber = 1;
            foreach (Round round in state.Schedule.Rounds)
            {
                int roomNumber = 0;
                DiscordChannel roundCategoryChannel = await context.Guild.CreateChannelAsync(
                    $"Round {roundNumber}", ChannelType.Category);

                foreach (Game game in round.Games)
                {
                    createTextChannelsTasks.Add(
                        CreateTextChannel(context, roundCategoryChannel, game, roles, roundNumber, roomNumber));
                    roomNumber++;
                }

                roundNumber++;
            }

            await Task.WhenAll(createTextChannelsTasks);
        }

        private static async Task<KeyValuePair<Team, DiscordRole>> CreateTeamRole(CommandContext context, Team team)
        {
            DiscordRole role = await context.Guild.CreateRoleAsync(GetTeamRoleName(team), color: DiscordColor.Aquamarine);
            return new KeyValuePair<Team, DiscordRole>(team, role);
        }

        private static async Task<DiscordChannel> CreateVoiceChannel(
            CommandContext context, TournamentRoles roles, Reader reader)
        {
            string name = GetVoiceRoomName(reader);
            DiscordChannel channel = await context.Guild.CreateChannelAsync(name, DSharpPlus.ChannelType.Voice);
            return channel;
        }

        private static async Task<Dictionary<Team, DiscordRole>> AssignPlayerRoles(
            CommandContext context, ITournamentState state, IDictionary<ulong, DiscordMember> members)
        {
            List<Task<KeyValuePair<Team, DiscordRole>>> addTeamRoleTasks = new List<Task<KeyValuePair<Team, DiscordRole>>>();
            foreach (Team team in state.Teams)
            {
                addTeamRoleTasks.Add(CreateTeamRole(context, team));
            }

            IEnumerable<KeyValuePair<Team, DiscordRole>> teamRolePairs = await Task.WhenAll(addTeamRoleTasks);
            Dictionary<Team, DiscordRole> teamRoles = new Dictionary<Team, DiscordRole>(teamRolePairs);

            List<Task> assignPlayerRoleTasks = new List<Task>();
            foreach (Player player in state.Players)
            {
                if (!teamRoles.TryGetValue(player.Team, out DiscordRole role))
                {
                    Console.Error.WriteLine($"Player {player.Id} does not have a team role for team {player.Team}.");
                    continue;
                }

                if (!members.TryGetValue(player.Id, out DiscordMember member))
                {
                    Console.Error.WriteLine($"Player {player.Id} does not have a DiscordMember.");
                    continue;
                }

                assignPlayerRoleTasks.Add(context.Guild.GrantRoleAsync(member, role));
            }

            await Task.WhenAll(assignPlayerRoleTasks);
            return teamRoles;
        }

        private static async Task<DiscordRole> AssignDirectorRole(
            CommandContext context,
            ITournamentState state,
            IDictionary<ulong, DiscordMember> members)
        {
            DiscordRole role = await context.Guild.CreateRoleAsync(
                DirectorRoleName, permissions: PrivilegedPermissions, color: DiscordColor.Gold);
            List<Task> assignRoleTasks = new List<Task>();
            foreach (ulong directorId in state.DirectorIds)
            {
                if (members.TryGetValue(directorId, out DiscordMember member))
                {
                    assignRoleTasks.Add(context.Guild.GrantRoleAsync(member, role));
                }
                else
                {
                    Console.Error.WriteLine($"Could not find director with ID {directorId}");
                }
            }

            await Task.WhenAll(assignRoleTasks);
            return role;
        }

        private static Task<DiscordRole[]> AssignRoomReaderRoles(
            CommandContext context, ITournamentState state, IDictionary<ulong, DiscordMember> members)
        {
            int roomsCount = state.Teams.Count() / 2;
            Task<DiscordRole>[] roomReaderRoleTasks = new Task<DiscordRole>[roomsCount];
            IEnumerator<Reader> readers = state.Readers.GetEnumerator();

            for (int i = 0; i < roomsCount && readers.MoveNext(); i++)
            {
                roomReaderRoleTasks[i] = AssignRoomReaderRole(
                    context, GetRoomReaderRoleName(i), readers.Current.Id, members);
            }

            return Task.WhenAll(roomReaderRoleTasks);
        }

        private static async Task<DiscordRole> AssignRoomReaderRole(
            CommandContext context, string roleName, ulong readerId, IDictionary<ulong, DiscordMember> members)
        {
            DiscordRole role = await context.Guild.CreateRoleAsync(
                roleName, permissions: PrivilegedPermissions, color: DiscordColor.Azure);
            await members[readerId].GrantRoleAsync(role, "Assigning reader role.");
            return role;
        }

        // TODO: Pass in the parent channel (category channel)
        // TODO: Pass in the Bot member
        private static async Task<DiscordChannel> CreateTextChannel(
            CommandContext context, DiscordChannel parent, Game game, TournamentRoles roles, int roundNumber, int roomNumber)
        {
            // The room and role names will be the same.
            string name = GetTextRoomName(game.Reader, roundNumber);
            DiscordChannel channel = await context.Guild.CreateChannelAsync(name, ChannelType.Text, parent);
            // Prevent people from seeing the text channel by default.
            await channel.AddOverwriteAsync(
                context.Guild.EveryoneRole,
                Permissions.None,
                Permissions.ReadMessageHistory | Permissions.AccessChannels);

            // TODO: Give the bot less-than-privileged permissions.
            // TODO: Pass in the bot member to reduce lookups
            DiscordMember botMember = await context.Guild.GetMemberAsync(context.Client.CurrentUser.Id);
            await channel.AddOverwriteAsync(botMember, PrivilegedPermissions, Permissions.None);

            await channel.AddOverwriteAsync(roles.DirectorRole, PrivilegedPermissions, Permissions.None);
            await channel.AddOverwriteAsync(roles.RoomReaderRoles[roomNumber], PrivilegedPermissions, Permissions.None);

            // They need to see the first message in the channel since the bot can't pin them. Since these are new
            // channels, this shouldn't matter.
            // TODO: We should pass in a dictionary of team roles, and set the channel to accept those roles.
            Permissions teamAllowedPermissions =
                Permissions.AccessChannels | Permissions.SendMessages | Permissions.ReadMessageHistory;
            // Need to get teams, then add allowedPermissions to this team

            List<Task> addTeamRolesToChannel = new List<Task>();
            foreach (Team team in game.Teams)
            {
                if (!roles.TeamRoles.TryGetValue(team, out DiscordRole role))
                {
                    Console.Error.WriteLine($"Team {team.Name} did not have a role defined.");
                    continue;
                }

                addTeamRolesToChannel.Add(channel.AddOverwriteAsync(role, teamAllowedPermissions, Permissions.None));
            }

            // TODO: Determine if admins need a separate role, or need to be granted permissions.

            await Task.WhenAll(addTeamRolesToChannel);
            return channel;
        }

        // Removes channels and roles.
        private static async Task CleanupTournamentArtifacts(CommandContext context, ITournamentState state)
        {
            // Simplest way is to delete all channels that are not a main channel
            // TODO: Store channel IDs in the state and delete those
            BotConfiguration configuration = context.Dependencies.GetDependency<BotConfiguration>();
            List<Task> deleteChannelsTask = new List<Task>();
            foreach (DiscordChannel channel in context.Guild.Channels)
            {
                // This should only be accepted on the main channel.
                if (channel != context.Channel)
                {
                    deleteChannelsTask.Add(channel.DeleteAsync("Tournament is over."));
                }
            }

            // TODO: Remove the channel roles
            // They all start with Role_. This will make sure that, even if a reader is removed from the list somehow,
            // we get all of the roles
            List<Task> deleteRolesTask = new List<Task>();

            // TODO: move to using the ids in the tournament state. This is useful until we have a way to clean up
            // artifacts from tournaments where the bot crashed.
            IReadOnlyList<DiscordRole> roles = context.Guild.Roles;
            foreach (DiscordRole role in roles)
            {
                string roleName = role.Name;
                if (roleName == DirectorRoleName ||
                    roleName.StartsWith(ReaderRoomRolePrefix) ||
                    roleName.StartsWith(TeamRolePrefix))
                {
                    deleteRolesTask.Add(context.Guild.DeleteRoleAsync(role));
                }
            }

            await Task.WhenAll(deleteChannelsTask);
            await Task.WhenAll(deleteRolesTask);
            await UpdateStage(context.Channel, state, TournamentStage.Complete);
        }

        private static Task DoReadActionOnCurrentTournament(
            CommandContext context, Func<IReadOnlyTournamentState, Task> action)
        {
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            return manager.DoReadActionOnCurrentTournamentForMember(context.Member, action);
        }

        private static Task DoReadWriteActionOnCurrentTournament(
            CommandContext context, Func<ITournamentState, Task> action)
        {
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            return manager.DoReadWriteActionOnCurrentTournamentForMember(context.Member, action);
        }

        private static async Task<string> GetTeamPlayersLine(
            CommandContext context, Tuple<Team, IEnumerable<Player>> teamPlayers)
        {
            DiscordMember[] members = await Task.WhenAll(teamPlayers.Item2
                .Select(player => context.Guild.GetMemberAsync(player.Id)));
            IEnumerable<string> names = members.Select(member => member.Nickname ?? member.DisplayName);
            return $"{teamPlayers.Item1.Name}: {string.Join(", ", names)}";
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

        private static bool IsMainChannel(CommandContext context)
        {
            BotConfiguration configuration = context.Dependencies.GetDependency<BotConfiguration>();
            return context.Channel.Name == configuration.MainChannelName;
        }

        private static bool IsAdminUser(CommandContext context, DiscordMember member)
        {
            return member.IsOwner ||
                (context.Channel.PermissionsFor(member) & Permissions.Administrator) == Permissions.Administrator;
        }

        private static bool HasTournamentDirectorPrivileges(CommandContext context)
        {
            // TD is only allowed to run commands when they are a director of the current tournament.
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            Result<bool> result = manager.TryReadActionOnCurrentTournament(currentTournament =>
                currentTournament.GuildId == context.Guild.Id &&
                (IsAdminUser(context, context.Member) || currentTournament.IsDirector(context.User.Id))
            );
            return result.Success && result.Value;
        }

        private static Task SendTournamentManagerUnavailableMessage(CommandContext context, string errorMessage)
        {
            return context.Channel.SendMessageAsync($"Unable to perform command. {errorMessage}");
        }

        private static async Task UpdateStage(DiscordChannel channel, ITournamentState state, TournamentStage stage)
        {
            state.UpdateStage(stage, out string title, out string instructions);
            if (title == null && instructions == null)
            {
                return;
            }

            DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
            embedBuilder.Title = title;
            embedBuilder.Description = instructions;
            await channel.SendMessageAsync(embed: embedBuilder.Build());
        }

        private class TournamentRoles
        {
            public DiscordRole DirectorRole { get; set; }

            public DiscordRole[] RoomReaderRoles { get; set; }

            public Dictionary<Team, DiscordRole> TeamRoles { get; set; }

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
