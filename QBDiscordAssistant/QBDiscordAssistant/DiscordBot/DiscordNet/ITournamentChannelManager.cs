using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public interface ITournamentChannelManager
    {
        Task<ITextChannel> CreateChannelsForFinals(
            ISelfUser botUser,
            ITournamentState state,
            Tournament.Game finalsGame,
            int finalsRoundNumber,
            int roomIndex);

        Task CreateChannelsForPrelims(
            ISelfUser botUser, ITournamentState state, TournamentRoles roles);

        Task CreateChannelsForRebracket(
            ISelfUser botUser, ITournamentState state, IEnumerable<Round> rounds, int startingRoundNumber);
    }
}