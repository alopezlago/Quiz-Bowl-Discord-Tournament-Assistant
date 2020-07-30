using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public interface ITournamentState : IReadOnlyTournamentState
    {
        new IEnumerable<ulong> ChannelIds { get; set; }
        new ulong? PinnedStartMessageId { get; set; }
        new int RoundRobinsCount { get; set; }
        new Schedule Schedule { get; set; }
        new TournamentRoleIds TournamentRoles { get; set; }

        void AddJoinTeamMessageId(ulong messageId);
        void AddSymbolToTeam(string symbol, Team team);
        void ClearJoinTeamMessageIds();
        void ClearSymbolsToTeam();
        bool TryAddDirector(ulong directorId);
        bool TryAddPlayer(Player player);
        void AddReaders(IEnumerable<Reader> readers);
        void AddTeams(IEnumerable<Team> teams);
        void RemoveTeams(IEnumerable<Team> teams);
        bool TryClearReaders();
        bool TryClearTeams();
        bool TryRemoveDirector(ulong directorId);
        bool TryRemovePlayer(ulong playerId);
        bool TryRemoveReader(ulong readerId);
        void UpdateStage(TournamentStage newStage, out string nextStageTitle, out string nextStageInstructions);
    }
}