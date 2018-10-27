using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using QBDiscordAssistant.Tournament;
using System;
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
                    Id = member.Id
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

                // TODO: Add bot initialization logic, where we set up the schedule and the channels.

                await context.Channel.SendMessageAsync("Starting the tournament...");
            }

            return;
        }

        [Command("end")]
        [Description("Ends the tournament.")]
        public Task End(CommandContext context, string rawTeamName)
        {
            if (IsMainChannel(context) && HasTournamentDirectorPrivileges(context))
            {
                TournamentsManager manager = context.Dependencies.GetDependency<TournamentsManager>();
                if (manager.CurrentTournament != null)
                {
                    string tournamentName = manager.CurrentTournament.Name;
                    manager.CurrentTournament = null;
                    return context.Channel.SendMessageAsync($"Tournament '{tournamentName}' has finished.");
                }
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
        // !win <team name> [reader] (Optional, do this only if we have time)


        public static bool IsMainChannel(CommandContext context)
        {
            BotConfiguration configuration = context.Dependencies.GetDependency<BotConfiguration>();
            return context.Channel.Name == configuration.MainChannelName;
        }

        public static bool IsAdminUser(CommandContext context)
        {
            BotPermissions permissions = context.Dependencies.GetDependency<BotPermissions>();
            return permissions.AdminIds.Contains(context.User.Id);
        }

        public static bool HasTournamentDirectorPrivileges(CommandContext context)
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
