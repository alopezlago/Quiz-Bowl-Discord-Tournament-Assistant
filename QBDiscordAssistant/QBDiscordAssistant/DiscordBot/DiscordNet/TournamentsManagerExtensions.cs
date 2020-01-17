using System;
using System.Threading.Tasks;
using Discord;
using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    internal static class TournamentsManagerExtensions
    {
        public static async Task DoReadActionOnCurrentTournamentForMemberAsync(
            this TournamentsManager manager, IUser user, Func<IReadOnlyTournamentState, Task> action)
        {
            Result<Task> result = manager.TryReadActionOnCurrentTournament(action);
            if (result.Success)
            {
                await result.Value;
                return;
            }

            IDMChannel channel = await user.GetOrCreateDMChannelAsync();
            await channel.SendMessageAsync(BotStrings.UnableToPerformCommand(result.ErrorMessage));
        }

        public static async Task DoReadWriteActionOnCurrentTournamentForMemberAsync(
            this TournamentsManager manager, IUser user, Func<ITournamentState, Task> action)
        {
            Result<Task> result = manager.TryReadWriteActionOnCurrentTournament(action);
            if (result.Success)
            {
                await result.Value;
                return;
            }

            IDMChannel channel = await user.GetOrCreateDMChannelAsync();
            await channel.SendMessageAsync(BotStrings.UnableToPerformCommand(result.ErrorMessage));
        }
    }
}
