namespace QBDiscordAssistant.Tournament
{
    public static class ITournamentStateExtensions
    {
        public static bool IsTorunamentInPlayStage(this IReadOnlyTournamentState tournamentState)
        {
            return tournamentState.Stage == TournamentStage.RunningPrelims ||
                tournamentState.Stage == TournamentStage.Finals;
        }
    }
}
