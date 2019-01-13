﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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
                if (!manager.PendingTournaments.TryGetValue(tournamentName, out TournamentState state))
                {
                    state = new TournamentState()
                    {
                        GuildId = context.Guild.Id,
                        Name = tournamentName
                    };

                    manager.PendingTournaments[tournamentName] = state;
                }

                // TODO: Need to handle this differently depending on the stage. Completed shouldn't do anything, and
                // after RoleSetup we should give them the TD role.
                if (state.TryAddDirector(newDirector.Id))
                {
                    return context.Channel.SendMessageAsync($"Added tournament director to tournament '{tournamentName}'.");
                }
                else
                {
                    return context.Channel.SendMessageAsync($"User is already a director of '{tournamentName}'.");
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
                if (manager.PendingTournaments.TryGetValue(tournamentName, out TournamentState state))
                {
                    if (state.TryRemoveDirector(newDirector.Id))
                    {
                        return context.Channel.SendMessageAsync(
                            $"Removed tournament director from tournament '{tournamentName}'.");
                    }
                    else
                    {
                        return context.Channel.SendMessageAsync(
                            "User is not a director for tournament '{tournamentName}'.");
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
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament != null)
                {
                    return context.Channel.SendMessageAsync($"Current tournament: {manager.CurrentTournament.Name}");
                }
                else
                {
                    return context.Channel.SendMessageAsync("No tournament is running.");
                }
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

            // TODO (Bug): Prevent the setup of two separate tournaments at the same time. This will require locking
            // the stage.
            string tournamentName = string.Join(" ", rawTournamentNameParts).Trim();
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            if (manager.PendingTournaments.TryGetValue(tournamentName, out TournamentState state) &&
                (IsAdminUser(context, context.Member) || state.IsDirector(context.User.Id)) &&
                manager.CurrentTournament == null)
            {
                // Once we enter setup we should remove the current tournament from pending
                // TODO: Consider moving this message to a constant.
                manager.CurrentTournament = state;
                manager.PendingTournaments.Remove(tournamentName);
                return UpdateStage(context.Channel, manager.CurrentTournament, TournamentStage.AddReaders);
            }

            return Task.CompletedTask;
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
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament.TryGetTeam(teamName, out Team team))
                {
                    Player player = new Player()
                    {
                        Id = member.Id,
                        Team = team
                    };
                    if (manager.CurrentTournament.TryAddPlayer(player))
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
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament.TryRemovePlayer(member.Id))
                {
                    return context.Member.SendMessageAsync($"Player {member.Mention} removed.");
                }
                else
                {
                    return context.Member.SendMessageAsync($"Player {member.Mention} is not on any team.");
                }
            }

            return Task.CompletedTask;
        }

        [Command("getPlayers")]
        [Description("Gets the players in the current tournament, grouped by their team.")]
        public async Task GetPlayers(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament == null)
                {
                    return;
                }

                IEnumerable<Tuple<Team, IEnumerable<Player>>> teamsAndPlayers = manager.CurrentTournament.Teams
                    .GroupJoin(
                        manager.CurrentTournament.Players,
                        team => team,
                        player => player.Team,
                        (team, players) => new Tuple<Team, IEnumerable<Player>>(team, players));

                // TODO: Look into using an embed. Embeds have 25-field limits, so use newlines in a message for now to
                // simplify the logic (no message splitting).
                string[] teamPlayerLines = await Task.WhenAll(teamsAndPlayers
                    .Select(teamPlayer => GetTeamPlayersLine(context, teamPlayer)));
                string content = string.Join(Environment.NewLine, teamPlayerLines);
                await context.Member.SendMessageAsync(content);
            }
        }


        [Command("start")]
        [Description("Starts the current tournament")]
        public async Task Start(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament?.Stage != TournamentStage.AddPlayers)
                {
                    // !start only applies once we've started adding players
                    return;
                }

                await UpdateStage(context.Channel, manager.CurrentTournament, TournamentStage.BotSetup);

                // TODO: Add more messaging around the current status
                IScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(manager.CurrentTournament.RoundRobinsCount);
                manager.CurrentTournament.Schedule = scheduleFactory.Generate(
                    new HashSet<Team>(manager.CurrentTournament.Teams),
                    new HashSet<Reader>(manager.CurrentTournament.Readers));

                await context.Channel.SendMessageAsync("Creating the channels and roles...");
                await CreateArtifacts(context, manager.CurrentTournament);

                await UpdateStage(context.Channel, manager.CurrentTournament, TournamentStage.RunningPrelims);
                await context.Channel.SendMessageAsync(
                    $"{context.Channel.Mention}: tournament has started.");
            }

            return;
        }

        [Command("back")]
        [Description("Undoes the current stage and returns to the previous stage.")]
        public async Task Back(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament == null)
                {
                    return;
                }

                switch (manager.CurrentTournament.Stage)
                {
                    case TournamentStage.SetRoundRobins:
                        manager.CurrentTournament.TryClearReaders();
                        break;
                    case TournamentStage.AddTeams:
                        manager.CurrentTournament.RoundRobinsCount = 0;
                        break;
                    case TournamentStage.AddPlayers:
                        manager.CurrentTournament.TryClearTeams();
                        manager.CurrentTournament.SymbolToTeam.Clear();

                        List<Task<DiscordMessage>> getJoinTeamMessagesTasks = new List<Task<DiscordMessage>>();
                        foreach (ulong id in manager.CurrentTournament.JoinTeamMessageIds)
                        {
                            getJoinTeamMessagesTasks.Add(context.Channel.GetMessageAsync(id));
                        }

                        DiscordMessage[] joinTeamMessages = await Task.WhenAll(getJoinTeamMessagesTasks);
                        await context.Channel.DeleteMessagesAsync(
                            joinTeamMessages, "Deleting join team messages since we are redoing the add teams stage.");

                        manager.CurrentTournament.JoinTeamMessageIds.Clear();
                        break;
                    default:
                        // Nothing to go back to, so do nothing.
                        await context.Member.SendMessageAsync(
                            $"Cannot go back from the stage {manager.CurrentTournament.Stage.ToString()}.");
                        return;
                }

                TournamentStage previousStage = manager.CurrentTournament.Stage - 1;
                await UpdateStage(context.Channel, manager.CurrentTournament, previousStage);
            }
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
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament?.Stage != TournamentStage.RunningPrelims)
                {
                    return;
                }

                if (!manager.CurrentTournament.TryGetReader(readerMember.Id, out Reader reader))
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
                if (manager.CurrentTournament.Teams.Intersect(teams).Count() != teams.Length)
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

                int finalsRoundNumber = manager.CurrentTournament.Schedule.Rounds.Count + 1;
                IList<Game> finalGames =
                    manager.CurrentTournament.Schedule.Rounds[manager.CurrentTournament.Schedule.Rounds.Count - 1].Games;
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
                DiscordRole directorRole = context.Guild.GetRole(manager.CurrentTournament.DirectorRoleId);
                DiscordRole[] roomReaderRoles = manager.CurrentTournament.ReaderRoomRoleIds
                    .Select(roleId => context.Guild.GetRole(roleId)).ToArray();
                Dictionary<Team, DiscordRole> teamRoles = manager.CurrentTournament.TeamRoleIds
                    .ToDictionary(kvp => kvp.Key, kvp => context.Guild.GetRole(kvp.Value));

                TournamentRoles tournamentRoles = new TournamentRoles()
                {
                    DirectorRole = directorRole,
                    RoomReaderRoles = roomReaderRoles,
                    TeamRoles = teamRoles
                };

                DiscordChannel finalsCategoryChannel = await context.Guild.CreateChannelAsync(
                    $"Finals", ChannelType.Category);
                DiscordChannel channel = await CreateTextChannel(
                    context,
                    finalsCategoryChannel,
                    finalsGame,
                    tournamentRoles,
                    finalsRoundNumber,
                    roomIndex);
                manager.CurrentTournament.UpdateStage(
                    TournamentStage.Finals, out string nextStageTitle, out string nextStageInstructions);
                await context.Channel.SendMessageAsync(
                    $"Finals participants: please join the room {channel.Name} and join the voice channel for that room number.");
            }
        }

        [Command("end")]
        [Description("Ends the current tournament.")]
        public async Task End(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament != null)
                {
                    // TODO: Add more messaging around the current status
                    await CleanupTournamentArtifacts(context, manager.CurrentTournament);

                    string tournamentName = manager.CurrentTournament.Name;
                    manager.CurrentTournament = null;
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

        private static async Task CreateArtifacts(CommandContext context, TournamentState state)
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
            DiscordRole[] roomReaderRoles = await CreateRoomReaderRoles(context, state);
            Dictionary<Team, DiscordRole> teamRoles = await AssignPlayerRoles(context, state, members);
            TournamentRoles roles = new TournamentRoles()
            {
                DirectorRole = directorRole,
                RoomReaderRoles = roomReaderRoles,
                TeamRoles = teamRoles
            };

            state.DirectorRoleId = directorRole.Id;
            state.ReaderRoomRoleIds = roomReaderRoles.Select(role => role.Id).ToArray();
            state.TeamRoleIds = teamRoles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id);

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
            CommandContext context, TournamentState state, IDictionary<ulong, DiscordMember> members)
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
            TournamentState state,
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

        private static Task<DiscordRole[]> CreateRoomReaderRoles(CommandContext context, TournamentState state)
        {
            int roomsCount = state.Teams.Count() / 2;
            Task<DiscordRole>[] roomReaderRoleTasks = new Task<DiscordRole>[roomsCount];

            for (int i = 0; i < roomsCount; i++)
            {
                roomReaderRoleTasks[i] =
                    context.Guild.CreateRoleAsync(
                        GetRoomReaderRoleName(i), permissions: PrivilegedPermissions, color: DiscordColor.LightGray);
            }

            return Task.WhenAll(roomReaderRoleTasks);
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
        private static async Task CleanupTournamentArtifacts(CommandContext context, TournamentState state)
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
            return manager.CurrentTournament != null &&
                manager.CurrentTournament.GuildId == context.Guild.Id &&
                (IsAdminUser(context, context.Member) || manager.CurrentTournament.IsDirector(context.User.Id));
        }

        private static async Task UpdateStage(DiscordChannel channel, TournamentState state, TournamentStage stage)
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
        }
    }
}
