using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public interface IReadOnlyTournamentState
    {
        IEnumerable<ulong> DirectorIds { get; }
        ulong GuildId { get; }
        IEnumerable<ulong> ChannelIds { get; }
        IEnumerable<ulong> JoinTeamMessageIds { get; }
        string Name { get; }
        IEnumerable<Player> Players { get; }
        IEnumerable<Reader> Readers { get; }
        int RoundRobinsCount { get; }
        // TODO: Find a way to restrict write-access to the Schedule instance (disable list access or AddRound access)
        Schedule Schedule { get; }
        TournamentStage Stage { get; }
        IEnumerable<Team> Teams { get; }
        TournamentRoleIds TournamentRoles { get; }

        bool IsDirector(ulong directorId);
        bool IsJoinTeamMessage(ulong messageId);
        bool IsReader(ulong readerId);
        bool TryGetPlayerTeam(ulong playerId, out Team team);
        bool TryGetReader(ulong readerId, out Reader reader);
        bool TryGetTeamFromName(string teamName, out Team team);
        bool TryGetTeamFromSymbol(string symbol, out Team team);
    }
}
