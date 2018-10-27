using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using QBDiscordAssistant.Tournament;
using System.Collections.Generic;
using System.Linq;
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

        [Command("addTD")]
        [Description("Adds a tournament director to a tournament, and creates that tournament if it doesn't exist yet.")]
        public Task AddTournamentDirector(CommandContext context, DiscordMember newDirector, string rawTournamentName)
        {
            if (IsMainChannel(context) && IsAdminUser(context))
            {
                string tournamentName = rawTournamentName.Trim();
                BotPermissions permissions = context.Dependencies.GetDependency<BotPermissions>();
                if (!permissions.PossibleDirectors.TryGetValue(tournamentName, out ISet<Director> directors))
                {
                    directors = new HashSet<Director>();
                    permissions.PossibleDirectors[tournamentName] = directors;
                    TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();

                    // TODO: Should we just store directors here instead of in BotPermissions?
                    manager.PendingTournaments[tournamentName] = new TournamentState()
                    {
                        Directors = directors.ToArray(),
                        GuildId = context.Guild.Id,
                        Name = tournamentName,
                        Stage = TournamentStage.Created
                    };
                }

                directors.Add(new Director()
                {
                    Id = newDirector.Id
                });

                return context.Channel.SendMessageAsync($"Added tournament director to tournament '{tournamentName}'.");
            }

            return Task.CompletedTask;
        }

        [Command("removeTD")]
        [Description("Removes a tournament director from a tournament.")]
        public Task RemoveTournamentDirector(CommandContext context, DiscordMember newDirector, string rawTournamentName)
        {
            if (IsMainChannel(context) && IsAdminUser(context))
            {
                string tournamentName = rawTournamentName.Trim();
                BotPermissions permissions = context.Dependencies.GetDependency<BotPermissions>();
                if (permissions.PossibleDirectors.TryGetValue(tournamentName, out ISet<Director> directors))
                {
                    permissions.PossibleDirectors[tournamentName].Remove(new Director()
                    {
                        Id = newDirector.Id
                    });
                }

                return context.Channel.SendMessageAsync($"Added tournament director to tournament '{tournamentName}'.");
            }

            return Task.CompletedTask;
        }

        [Command("getCurrentTournament")]
        [Description("Gets the name of the current tournament, if it exists.")]
        public Task GetCurrentTournament(CommandContext context, string rawTournamentName)
        {
            if (IsMainChannel(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament != null)
                {
                    return context.Channel.SendMessageAsync($"Current tournament name is: {manager.CurrentTournament.Name}");
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
        public Task Setup(CommandContext context, string rawTournamentName)
        {
            if (!IsMainChannel(context))
            {
                return Task.CompletedTask;
            }

            // We really need to refactor permissions so it's just the ID.
            string tournamentName = rawTournamentName.Trim();
            BotPermissions permissions = context.Dependencies.GetDependency<BotPermissions>();
            if (IsAdminUser(context) ||
                (permissions.PossibleDirectors.TryGetValue(tournamentName, out ISet<Director> directors) &&
                directors.Contains(new Director()
                {
                    Id = context.User.Id
                })))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament == null &&
                    manager.PendingTournaments.TryGetValue(tournamentName, out TournamentState state))
                {
                    // Once we enter setup we should remove the current tournament from pending
                    manager.CurrentTournament = state;
                    manager.PendingTournaments.Remove(tournamentName);
                    state.Stage = TournamentStage.RoleSetup;
                    return context.Channel.SendMessageAsync(
                        $"Begin setup phase for '{tournamentName}'. Add readers with !addreaders @user, add teams with !addteams <team>, and set the number of round robins with !roundrobins <# of round robins>. Once all players have joined with !joinTeam <team>, begin the tournament with !start.");
                }
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
        public Task AddTeam(CommandContext context, string rawTeamName)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament.Teams.Add(new Team()
                {
                    Name = rawTeamName.Trim()
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
        public Task RemoveTeam(CommandContext context, string rawTeamName)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament.Teams.Remove(new Team()
                {
                    Name = rawTeamName.Trim()
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
        public Task AddPlayer(CommandContext context, DiscordMember member, string rawTeamName)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                string teamName = rawTeamName.Trim();
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
        public Task JoinTeam(CommandContext context, string rawTeamName)
        {
            if (IsMainChannel(context))
            {
                string teamName = rawTeamName.Trim();
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
        public Task LeaveTeam(CommandContext context, string rawTeamName)
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

        [Command("roundRobins")]
        [Description("Sets the number of round robins to run.")]
        public Task End(CommandContext context, int roundRobinsCount)
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
        [Description("Starts the tournament")]
        public async Task Start(CommandContext context, int roundRobinsCount)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                manager.CurrentTournament.Stage = TournamentStage.BotSetup;
                await context.Channel.SendMessageAsync("Initializing the schedule...");

                // TODO: We should try to move this out of the Bot class.
                IScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(manager.CurrentTournament.RoundRobinsCount);
                manager.CurrentTournament.Schedule = scheduleFactory.Generate(
                    manager.CurrentTournament.Teams, manager.CurrentTournament.Readers);
                await CreateChannels(context, manager.CurrentTournament);

                manager.CurrentTournament.Stage = TournamentStage.Running;
                await context.Channel.SendMessageAsync("Starting the tournament...");
            }

            return;
        }

        [Command("end")]
        [Description("Ends the tournament.")]
        public async Task End(CommandContext context, string rawTeamName)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament != null)
                {
                    await CleanupTournamentArtifcats(context);

                    string tournamentName = manager.CurrentTournament.Name;
                    manager.CurrentTournament = null;
                    await context.Channel.SendMessageAsync($"Tournament '{tournamentName}' has finished.");
                }
            }
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
        // !win <team name> [reader] (Optional, do this only if we have time)

        private static async Task CreateChannels(CommandContext context, TournamentState state)
        {
            // Create the reader role
            DiscordRole readerRole = await context.Guild.CreateRoleAsync("Reader", color: DiscordColor.Orange, permissions:
                Permissions.UseVoiceDetection |
                Permissions.UseVoice |
                Permissions.Speak |
                Permissions.SendMessages |
                Permissions.KickMembers |
                Permissions.MuteMembers);

            // Create the voice channels
            List<Task> createChannelsTasks = new List<Task>();
            ////List<Task> deleteExistingChannelsTasks = new List<Task>();
            foreach (Reader reader in state.Readers)
            {
                // TODO: See what happens if we try to create an existing channel. If it's bad, then delete channels
                // whose names overlap.
                createChannelsTasks.Add(CreateVoiceChannel(context, reader, readerRole));
            }

            // Create the text channels
            int roundNumber = 1;
            foreach (Round round in state.Schedule.Rounds)
            {
                foreach (Game game in round.Games)
                {
                    createChannelsTasks.Add(CreateTextChannel(context, state, game, roundNumber));
                }

                roundNumber++;
            }

            await Task.WhenAll(createChannelsTasks);
            await context.Channel.SendMessageAsync("Tournament channels have been created.");
        }

        private static async Task CreateVoiceChannel(CommandContext context, Reader reader, DiscordRole readerRole)
        {
            string name = $"{reader.Name}'s_Voice_Room";
            DiscordChannel channel = await context.Guild.CreateChannelAsync(name, DSharpPlus.ChannelType.Voice);
            DiscordMember readerMember = await context.Guild.GetMemberAsync(reader.Id);
            await context.Guild.GrantRoleAsync(readerMember, readerRole);
        }

        private static async Task CreateTextChannel(
            CommandContext context, TournamentState state, Game game, int roundNumber)
        {
            // The room and role names will be the same.
            string name = GetTextRoomName(game.Reader, roundNumber);
            DiscordChannel channel = await context.Guild.CreateChannelAsync(name, DSharpPlus.ChannelType.Text);
            DiscordRole roomRole = await context.Guild.CreateRoleAsync(name);
            await channel.AddOverwriteAsync(context.Guild.EveryoneRole, Permissions.None, Permissions.None);
            await channel.AddOverwriteAsync(
                roomRole, Permissions.AccessChannels | Permissions.SendMessages, Permissions.None);

            // Grants the room role to the players, reader, and the admins.
            DiscordMember readerMember = await context.Guild.GetMemberAsync(game.Reader.Id);
            List<Task> grantRoomRole = new List<Task>();
            grantRoomRole.Add(context.Guild.GrantRoleAsync(readerMember, roomRole));
            IEnumerable<DiscordMember> admins = context.Guild.Members
                .Where(member => member.Roles
                    .Any(role => role.CheckPermission(Permissions.Administrator) == PermissionLevel.Allowed));
            foreach (DiscordMember admin in admins)
            {
                grantRoomRole.Add(context.Guild.GrantRoleAsync(admin, roomRole));
            }

            IEnumerable<DiscordMember> playerMembers = await Task.WhenAll(state.Players
                .Join(game.Teams, player => player.Team, team => team, (player, team) => player.Id)
                .Select(id => context.Guild.GetMemberAsync(id)));
            foreach (DiscordMember playerMember in playerMembers)
            {
                grantRoomRole.Add(context.Guild.GrantRoleAsync(playerMember, roomRole));
            }

            await Task.WhenAll(grantRoomRole);
        }

        // Removes channels and roles.
        private static async Task CleanupTournamentArtifcats(CommandContext context)
        {
            TournamentState state = context.Dependencies.GetDependency<TournamentState>();
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
            foreach (DiscordRole role in context.Guild.Roles.Where(r => r.Name.StartsWith("Round_")))
            {
                deleteRolesTask.Add(context.Guild.DeleteRoleAsync(role));
            }

            await Task.WhenAll(deleteChannelsTask);
            await Task.WhenAll(deleteRolesTask);
            await context.Channel.SendMessageAsync("All tournament channels and roles removed.");
        }

        private static string GetTextRoomName(Reader reader, int roundNumber)
        {
            return $"Round_{roundNumber}_{reader.Name.Replace(" ", "_")}";
        }

        private static bool IsMainChannel(CommandContext context)
        {
            BotConfiguration configuration = context.Dependencies.GetDependency<BotConfiguration>();
            return context.Channel.Name == configuration.MainChannelName;
        }

        // TODO: Look into using "RequiredRole" instead of this.
        private static bool IsAdminUser(CommandContext context)
        {
            BotPermissions permissions = context.Dependencies.GetDependency<BotPermissions>();
            return permissions.AdminIds.Contains(context.User.Id);
        }

        private static bool HasTournamentDirectorPrivileges(CommandContext context)
        {
            if (IsAdminUser(context))
            {
                return true;
            }

            BotPermissions permissions = context.Dependencies.GetDependency<BotPermissions>();
            TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
            if (manager.CurrentTournament == null)
            {
                // No tournament is running.
                return false;
            }

            return permissions.PossibleDirectors.TryGetValue(manager.CurrentTournament.Name, out ISet<Director> directors) &&
                directors.Contains(new Director()
                {
                    Id = context.User.Id
                });
        }
    }
}
