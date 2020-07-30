namespace QBDiscordAssistant.Tournament
{
    public static class ITournamentStateExtensions
    {
        public static bool IsTorunamentInPlayStage(this IReadOnlyTournamentState tournamentState)
        {
            return tournamentState != null &&
                (tournamentState.Stage == TournamentStage.RunningTournament ||
                    tournamentState.Stage == TournamentStage.Rebracketing ||
                    tournamentState.Stage == TournamentStage.Finals);
        }
    }
}
