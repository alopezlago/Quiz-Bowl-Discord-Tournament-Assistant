using System;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.Extensions.DependencyInjection;
using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
    public class RequireTournamentDirectorAttribute : PreconditionAttribute
    {
        private const string SetupCommandName = "setup";
        private const string SetupCommand = "!" + SetupCommandName;

        public override Task<PreconditionResult> CheckPermissionsAsync(
            ICommandContext context, CommandInfo command, IServiceProvider serviceProvider)
        {
            Verify.IsNotNull(context, nameof(context));

            GlobalTournamentsManager globalManager = serviceProvider.GetService<GlobalTournamentsManager>();
            TournamentsManager manager = globalManager.GetOrAdd(context.Guild.Id, CreateTournamentsManager);

            // TD is only allowed to run commands when they are a director of the current tournament.
            Result<bool> result = manager.TryReadActionOnCurrentTournament(currentTournament =>
                currentTournament.GuildId == context.Guild.Id && CanActAsTournamentDirector(context, currentTournament)
            );
            if (result.Success && result.Value)
            {
                return Task.FromResult(PreconditionResult.FromSuccess());
            }
            else if (command.Name == SetupCommandName && context.Message.Content.Length > SetupCommand.Length)
            {
                // TODO: We should investigate if there's a better place to make this check, because the attribute
                // now knows about "setup"
                string tournamentName = context.Message.Content.Substring(SetupCommand.Length).Trim();
                if (manager.TryGetTournament(tournamentName, out ITournamentState tournament) &&
                    CanActAsTournamentDirector(context, tournament))
                {
                    return Task.FromResult(PreconditionResult.FromSuccess());
                }

            }

            return Task.FromResult(PreconditionResult.FromError("User did not have tournament director privileges."));
        }

        private static TournamentsManager CreateTournamentsManager(ulong id)
        {
            TournamentsManager manager = new TournamentsManager()
            {
                GuildId = id
            };
            return manager;
        }

        private static bool IsAdminUser(ICommandContext context)
        {
            return context.User is IGuildUser guildUser &&
                (guildUser.Guild.OwnerId == guildUser.Id || guildUser.GuildPermissions.Administrator);
        }

        private static bool CanActAsTournamentDirector(ICommandContext context, IReadOnlyTournamentState tournament)
        {
            return IsAdminUser(context) || tournament.IsDirector(context.User.Id);
        }
    }
}
