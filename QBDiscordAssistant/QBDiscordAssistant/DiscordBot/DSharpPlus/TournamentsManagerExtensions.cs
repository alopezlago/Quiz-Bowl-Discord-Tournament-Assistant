using DSharpPlus.Entities;
using QBDiscordAssistant.Tournament;
using System;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DSharpPlus
{
    internal static class TournamentsManagerExtensions
    {
        public static Task DoReadActionOnCurrentTournamentForMember(
            this TournamentsManager manager, DiscordMember member, Func<IReadOnlyTournamentState, Task> action)
        {
            Result<Task> result = manager.TryReadActionOnCurrentTournament(action);
            return result.Success ?
                result.Value :
                member.SendMessageAsync($"Unable to perform command. {result.ErrorMessage}");
        }

        public static Task DoReadWriteActionOnCurrentTournamentForMember(
            this TournamentsManager manager, DiscordMember member, Func<ITournamentState, Task> action)
        {
            Result<Task> result = manager.TryReadWriteActionOnCurrentTournament(action);
            return result.Success ?
                result.Value :
                member.SendMessageAsync($"Unable to perform command. {result.ErrorMessage}");
        }
    }
}
