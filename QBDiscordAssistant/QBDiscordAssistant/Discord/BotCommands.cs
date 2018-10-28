﻿using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using QBDiscordAssistant.Tournament;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBDiscordAssistant.Discord
{
    public class BotCommands
    {
        private readonly BotCommandHandler handler;

        public BotCommands()
        {
            this.handler = new BotCommandHandler();
        }

        // TODO: Move all of the implementation to the BotCommandHandler methods so that they can be unit tested.
        // TODO: Instead of sending confirmations to the channel, maybe send then to the user who did them? One issue is
        // that they're not going to be looking at their DMs.
        // TODO: Look into abstracting away some of the "get members of X". Should simplify the permissions setting.
        // TODO: Directors still aren't able to see channels.

        // TODO: strings are split by spaces, so we need to get the parameter from the raw string.
        [Command("addTD")]
        [Description("Adds a tournament director to a tournament, and creates that tournament if it doesn't exist yet.")]
        [RequireOwner]
        [RequirePermissions(Permissions.Administrator)]
        public Task AddTournamentDirector(
            CommandContext context, DiscordMember newDirector, params string[] tournamentNameParts)
        {
            if (IsMainChannel(context))
            {
                string tournamentName = string.Join(" ", tournamentNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (!manager.PendingTournaments.TryGetValue(tournamentName, out TournamentState state))
                {
                    state = new TournamentState()
                    {
                        GuildId = context.Guild.Id,
                        Name = tournamentName,
                        Stage = TournamentStage.Created
                    };

                    manager.PendingTournaments[tournamentName] = state;
                }

                if (state.DirectorIds.Add(newDirector.Id))
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
            CommandContext context, DiscordMember newDirector, params string[] tournamentNameParts)
        {
            if (IsMainChannel(context))
            {
                string tournamentName = string.Join(" ", tournamentNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.PendingTournaments.TryGetValue(tournamentName, out TournamentState state))
                {
                    if (state.DirectorIds.Remove(newDirector.Id))
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
        public Task Setup(CommandContext context, params string[] rawTournamentNameParts)
        {
            if (!IsMainChannel(context))
            {
                return Task.CompletedTask;
            }

            string tournamentName = string.Join(" ", rawTournamentNameParts).Trim();
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            if (manager.PendingTournaments.TryGetValue(tournamentName, out TournamentState state) &&
                (IsAdminUser(context) || state.DirectorIds.Contains(context.User.Id)) &&
                manager.CurrentTournament == null)
            {
                // Once we enter setup we should remove the current tournament from pending
                // TODO: Consider moving this message to a constant.
                manager.CurrentTournament = state;
                manager.PendingTournaments.Remove(tournamentName);
                state.Stage = TournamentStage.RoleSetup;
                StringBuilder builder = new StringBuilder();
                builder.AppendLine($"Begin setup phase for '{tournamentName}'");
                builder.AppendLine("Add readers with !addReader *@user*");
                builder.AppendLine("Add teams with !addTeam *team*");
                builder.AppendLine("Set the number of round robins with !setRoundRobins *# of round robins*");
                builder.AppendLine("Players can join their team with !jointeam *team*");
                builder.AppendLine("Once everything is set up, the director beings the tournament with !start");

                return context.Channel.SendMessageAsync(builder.ToString());
            }

            return Task.CompletedTask;
        }

        [Command("addReader")]
        [Description("Add a reader.")]
        public Task AddReader(CommandContext context, DiscordMember member)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                manager.CurrentTournament.Readers.Add(new Reader()
                {
                    Id = member.Id,
                    Name = member.Nickname ?? member.DisplayName
                });
                return context.Channel.SendMessageAsync("Reader added.");
            }

            return Task.CompletedTask;
        }

        [Command("removeReader")]
        [Description("Removes a reader.")]
        public Task RemovesReader(CommandContext context, DiscordMember member)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                manager.CurrentTournament.Readers.Remove(new Reader()
                {
                    // We don't need the name to remove it.
                    Id = member.Id
                });
                return context.Channel.SendMessageAsync("Reader removed.");
            }

            return Task.CompletedTask;
        }

        [Command("addTeam")]
        [Description("Add a team.")]
        public Task AddTeam(CommandContext context, params string[] rawTeamNameParts)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                string teamName = string.Join(" ", rawTeamNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament.Teams.Add(new Team()
                {
                    Name = teamName
                }))
                {
                    return context.Channel.SendMessageAsync("Team added.");
                }
                else
                {
                    return context.Channel.SendMessageAsync("Team already exists.");
                }
            }

            return Task.CompletedTask;
        }

        [Command("removeTeam")]
        [Description("Removes a team.")]
        public Task RemoveTeam(CommandContext context, params string[] rawTeamNameParts)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                string teamName = string.Join(" ", rawTeamNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament.Teams.Remove(new Team()
                {
                    Name = teamName
                }))
                {
                    return context.Channel.SendMessageAsync("Team removed.");
                }
                else
                {
                    return context.Channel.SendMessageAsync("Team does not exist.");
                }
            }

            return Task.CompletedTask;
        }

        [Command("addPlayer")]
        [Description("Adds a player to a team.")]
        public Task AddPlayer(CommandContext context, DiscordMember member, params string[] rawTeamNameParts)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                string teamName = string.Join(" ", rawTeamNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                Team team = new Team()
                {
                    Name = teamName
                };
                if (manager.CurrentTournament.Teams.Contains(team))
                {
                    if (manager.CurrentTournament.Players.Add(new Player()
                    {
                        Id = member.Id,
                        Team = team
                    }))
                    {
                        return context.Channel.SendMessageAsync("Player added.");
                    }
                    else
                    {
                        return context.Channel.SendMessageAsync("Player is already on a team.");
                    }
                }
                else
                {
                    return context.Channel.SendMessageAsync("Team does not exist.");
                }
            }

            return Task.CompletedTask;
        }

        [Command("removePlayer")]
        [Description("Removes a player from a team.")]
        public Task RemovePlayer(CommandContext context, DiscordMember member)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                Player player = new Player()
                {
                    Id = member.Id
                };
                if (manager.CurrentTournament.Players.Remove(player))
                {
                    return context.Channel.SendMessageAsync("Player removed.");
                }
                else
                {
                    return context.Channel.SendMessageAsync("Player is not on any team.");
                }
            }

            return Task.CompletedTask;
        }

        // TODO: Refactor so that this shares the same code as addPlayer/removePlayer
        [Command("joinTeam")]
        [Description("Join a team.")]
        public Task JoinTeam(CommandContext context, params string[] rawTeamNameParts)
        {
            if (IsMainChannel(context))
            {
                string teamName = string.Join(" ", rawTeamNameParts).Trim();
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                Team team = new Team()
                {
                    Name = teamName
                };
                if (manager.CurrentTournament.Teams.Contains(team))
                {
                    if (manager.CurrentTournament.Players.Add(new Player()
                    {
                        Id = context.User.Id,
                        Team = team
                    }))
                    {
                        return context.Channel.SendMessageAsync($"{context.User.Mention} joined team.");
                    }
                    else
                    {
                        return context.Channel.SendMessageAsync($"{context.User.Mention} is already on a team. Use !leaveteam to drop out.");
                    }
                }
                else
                {
                    return context.Channel.SendMessageAsync("Team does not exist.");
                }
            }

            return Task.CompletedTask;
        }

        [Command("leaveTeam")]
        [Description("Leave a team.")]
        public Task LeaveTeam(CommandContext context)
        {
            if (IsMainChannel(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                Player player = new Player()
                {
                    Id = context.User.Id
                };
                if (manager.CurrentTournament.Players.Remove(player))
                {
                    return context.Channel.SendMessageAsync($"{context.User.Mention} left their team.");
                }
                else
                {
                    return context.Channel.SendMessageAsync($"{context.User.Mention} is not on any team.");
                }
            }

            return Task.CompletedTask;
        }

        [Command("setRoundRobins")]
        [Description("Sets the number of round robins to run.")]
        public Task SetRoundRobins(CommandContext context, int roundRobinsCount)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                manager.CurrentTournament.RoundRobinsCount = roundRobinsCount;
                return context.Channel.SendMessageAsync("Round robins set.");
            }

            return Task.CompletedTask;
        }

        [Command("start")]
        [Description("Starts the current tournament")]
        public async Task Start(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (!(await IsTournamentReady(context, manager.CurrentTournament)))
                {
                    // IsTournamentReady handles the messaging since it knows what is invalid.
                    return;
                }

                manager.CurrentTournament.Stage = TournamentStage.BotSetup;
                await context.Channel.SendMessageAsync("Initializing the schedule...");

                // TODO: We should try to move this out of the Bot class.
                IScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(manager.CurrentTournament.RoundRobinsCount);
                manager.CurrentTournament.Schedule = scheduleFactory.Generate(
                    manager.CurrentTournament.Teams, manager.CurrentTournament.Readers);

                await context.Channel.SendMessageAsync("Creating the channels and roles...");
                await CreateChannels(context, manager.CurrentTournament);

                manager.CurrentTournament.Stage = TournamentStage.Running;
                await context.Channel.SendMessageAsync(
                    $"{context.Channel.Mention}: tournament has started. Go to your first round room and follow the instructions.");
            }

            return;
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
                    await CleanupTournamentArtifcats(context, manager.CurrentTournament);

                    string tournamentName = manager.CurrentTournament.Name;
                    manager.CurrentTournament = null;
                }
            }
        }

        [Command("quick")]
        [Description("Delete. Adds default teams and round robins. Still needs assigned reader and added players.")]
        public Task Quick(CommandContext context)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                manager.CurrentTournament.Teams.Add(new Team()
                {
                    Name = "Team1"
                });
                manager.CurrentTournament.Teams.Add(new Team()
                {
                    Name = "Team2"
                });
                manager.CurrentTournament.RoundRobinsCount = 2;
            }

            return Task.CompletedTask;
        }

        // Commands:
        // Note that Admins can perform all TD actions.
        // We may want to add range methods to make it easier, e.g. !addReaders @user1, @user2, @user3, etc. Need to see
        // if Discord supports this.
        // Also, right now we force users to !leave/!remove and then !add/!join again. We should consider automatically
        // removing them so that we don't need to support both commands.
        // We may want to guide TD through commands. So tell them after !addTeams, "now add users", after !roundrobins, "!start", etc.
        // 
        // Only in the main room:
        // !addTD @user <tournament name> [Admin]
        // !removeTD @user <tournament name> [Admin]
        // !getCurrentTournament [Any]
        // !setup [TD]
        // !addReader @user [TD]
        // !removeReader @user [TD]
        // !addTeam <team name> [TD]
        // !addPlayer @user <team name> [TD]
        // !joinTeam <team name> [player]
        // !removePlayer @user [TD]
        // !roundrobin <number> [TD] (should do single/double/triple round robin)
        // !nofinal [TD] (TODO later, sets no final, when we move to advantaged final by default)
        // !start [TD]
        // !end [TD, Admin]
        //
        // !win <team name> [reader] (TODO later, do this only if we have time.)

        private static async Task<bool> IsTournamentReady(CommandContext context, TournamentState state)
        {
            // TODO: Consider using a DiscordEmbed for this to make it look nicer.
            StringBuilder failures = new StringBuilder();
            if (state.Readers.Count == 0)
            {
                failures.AppendLine("- No readers assigned. Add readers with !addReader *@user mention*");
            }

            if (state.Teams.Count < 2)
            {
                failures.AppendLine(
                    $"- Tournaments need 2 teams but only {state.Teams.Count} were created. Add teams with !addTeam *team name*");
            }

            if (state.RoundRobinsCount <= 0)
            {
                failures.AppendLine("- The number of round robins to play is not set. Set it with !setRoundRobins *number*");
            }

            // TODO: Add player validation

            if (failures.Length > 0)
            {
                await context.Channel.SendMessageAsync(failures.ToString());
                return false;
            }

            return true;
        }

        private static async Task CreateChannels(CommandContext context, TournamentState state)
        {
            // Create the reader role
            // 256 according to the Discord documentation
            Permissions prioritySpeaker = (Permissions)256;
            DiscordRole readerRole = await context.Guild.CreateRoleAsync("Reader", color: DiscordColor.Orange, permissions:
                Permissions.UseVoiceDetection |
                Permissions.UseVoice |
                Permissions.Speak |
                Permissions.SendMessages |
                Permissions.KickMembers |
                Permissions.MuteMembers |
                Permissions.DeafenMembers |
                Permissions.ReadMessageHistory |
                Permissions.AccessChannels |
                prioritySpeaker);

            // Create the voice channels
            List<Task<DiscordChannel>> createVoiceChannelsTasks = new List<Task<DiscordChannel>>();
            ////List<Task> deleteExistingChannelsTasks = new List<Task>();
            foreach (Reader reader in state.Readers)
            {
                // TODO: See what happens if we try to create an existing channel. If it's bad, then delete channels
                // whose names overlap.
                createVoiceChannelsTasks.Add(CreateVoiceChannel(context, reader, readerRole));
            }

            // We will need the voice channels to get the mentions for it that we post when the user joins the
            // text channel.
            IDictionary<string, DiscordChannel> voiceChannels = (await Task.WhenAll(createVoiceChannelsTasks))
                .ToDictionary(c => c.Name, c => c);

            // Create the text channels
            List<Task> createTextChannelsTasks = new List<Task>();
            int roundNumber = 1;
            foreach (Round round in state.Schedule.Rounds)
            {
                foreach (Game game in round.Games)
                {
                    createTextChannelsTasks.Add(CreateTextChannel(context, state, voiceChannels, game, roundNumber));
                }

                roundNumber++;
            }

            await Task.WhenAll(createTextChannelsTasks);
        }

        private static async Task<DiscordChannel> CreateVoiceChannel(
            CommandContext context, Reader reader, DiscordRole readerRole)
        {
            string name = GetVoiceRoomName(reader);
            DiscordChannel channel = await context.Guild.CreateChannelAsync(name, DSharpPlus.ChannelType.Voice);
            DiscordMember readerMember = await context.Guild.GetMemberAsync(reader.Id);
            await context.Guild.GrantRoleAsync(readerMember, readerRole);
            return channel;
        }

        private static async Task CreateTextChannel(
            CommandContext context,
            TournamentState state,
            IDictionary<string, DiscordChannel> voiceChannels,
            Game game,
            int roundNumber)
        {
            // The room and role names will be the same.
            string name = GetTextRoomName(game.Reader, roundNumber);
            DiscordChannel channel = await context.Guild.CreateChannelAsync(name, DSharpPlus.ChannelType.Text);
            DiscordRole roomRole = await context.Guild.CreateRoleAsync(name);
            await channel.AddOverwriteAsync(context.Guild.EveryoneRole, Permissions.None, Permissions.ReadMessageHistory | Permissions.AccessChannels);

            // They need to see the first message in the channel since the bot can't pin them. Since these are new
            // channels, this shouldn't matter.
            Permissions allowedPermissions =
                Permissions.AccessChannels | Permissions.SendMessages | Permissions.ReadMessageHistory;
            await channel.AddOverwriteAsync(roomRole, allowedPermissions, Permissions.None);

            // TODO TODO: Reader doesn't seem to get permission to the text channels!
            // Grants the room role to the players, reader, and the admins.
            DiscordMember readerMember = await context.Guild.GetMemberAsync(game.Reader.Id);
            List<Task> grantRoomRole = new List<Task>();
            grantRoomRole.Add(context.Guild.GrantRoleAsync(readerMember, roomRole));

            // Make sure the bot has visibility
            DiscordMember botMember = await context.Guild.GetMemberAsync(context.Client.CurrentUser.Id);
            await context.Guild.GrantRoleAsync(botMember, roomRole);

            IReadOnlyList<DiscordMember> members = context.Guild.Members;

            IEnumerable<DiscordMember> admins = members
                .Where(member => member.Roles
                    .Any(role => role.CheckPermission(Permissions.Administrator) == PermissionLevel.Allowed));
            foreach (DiscordMember admin in admins)
            {
                grantRoomRole.Add(context.Guild.GrantRoleAsync(admin, roomRole));
            }

            IEnumerable<DiscordMember> directors = members.Where(member => state.DirectorIds.Contains(member.Id));
            foreach (DiscordMember director in directors)
            {
                grantRoomRole.Add(context.Guild.GrantRoleAsync(director, roomRole));
            }

            // TODO: Use members instead?
            IEnumerable<DiscordMember> playerMembers = await Task.WhenAll(state.Players
                .Join(game.Teams, player => player.Team, team => team, (player, team) => player.Id)
                .Select(id => context.Guild.GetMemberAsync(id)));
            foreach (DiscordMember playerMember in playerMembers)
            {
                grantRoomRole.Add(context.Guild.GrantRoleAsync(playerMember, roomRole));
            }

            await Task.WhenAll(grantRoomRole);

            string channelMention = voiceChannels[GetVoiceRoomName(game.Reader)].Mention;
            await channel.SendMessageAsync($"Players: join this voice channel: {channelMention}");
        }

        // Removes channels and roles.
        private static async Task CleanupTournamentArtifcats(CommandContext context, TournamentState state)
        {
            // Simplest way is to delete all channels that are not a main channel
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

            // Remove the reader role
            DiscordRole readerRole = context.Guild.Roles.FirstOrDefault(r => r.Name == "Reader");
            if (readerRole != null)
            {
                await context.Guild.DeleteRoleAsync(readerRole);
            }

            // TODO: Remove the channel roles
            // They all start with Role_. This will make sure that, even if a reader is removed from the list somehow,
            // we get all of the roles
            List<Task> deleteRolesTask = new List<Task>();
            
            // We need this out of the loop because the role could be deleted while we are enumerating through it.
            DiscordRole[] roomRoles = context.Guild.Roles.Where(r => r.Name.StartsWith("Round_")).ToArray();
            foreach (DiscordRole role in roomRoles)
            {
                deleteRolesTask.Add(context.Guild.DeleteRoleAsync(role));
            }

            await Task.WhenAll(deleteChannelsTask);
            await Task.WhenAll(deleteRolesTask);
            await context.Channel.SendMessageAsync(
                $"All tournament channels and roles removed. Tournament '{state.Name}' is now finished.");
        }

        private static string GetTextRoomName(Reader reader, int roundNumber)
        {
            return $"Round_{roundNumber}_{reader.Name.Replace(" ", "_")}";
        }

        private static string GetVoiceRoomName(Reader reader)
        {
            return $"{reader.Name.Replace(" ", "_")}'s_Voice_Room";
        }

        private static bool IsMainChannel(CommandContext context)
        {
            BotConfiguration configuration = context.Dependencies.GetDependency<BotConfiguration>();
            return context.Channel.Name == configuration.MainChannelName;
        }

        private static bool IsAdminUser(CommandContext context)
        {
            return context.Member.IsOwner ||
                (context.Channel.PermissionsFor(context.Member) & Permissions.Administrator) == Permissions.Administrator;
        }

        private static bool HasTournamentDirectorPrivileges(CommandContext context)
        {
            if (IsAdminUser(context))
            {
                return true;
            }

            // TD is only allowed to run commands when they are a director of the current tournament.
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            return manager.CurrentTournament != null && manager.CurrentTournament.DirectorIds.Contains(context.User.Id);
        }
    }
}
