namespace QBDiscordAssistant.Tournament
{
    public static class TournamentStrings
    {
        public const string AddReaders = "Add Readers";
        public const string AddTeams = "Add Teams";
        public const string InitializingSchedule = "Initializing the schedule. Channels and roles will be set up next.";
        public const string ListMentionsOfAllReaders = "List the mentions of all of the readers. For example, '@Reader_1 @Reader_2 @Reader_3'. If you forgot a reader, you can still use !addReaders during the add teams phase.";
        public const string MustHaveMoreThanOneTeamPerBracket = "Must have more than 1 team per bracket.";
        public const string MustHaveReader = "Must have a reader.";
        public const string NoCurrentTournamentRunning = "No current tournament is running.";
        public const string SetNumberRoundRobins = "Set the number of round robins.";
        public const string SettingUpTournament = "Setting up the tournament.";
        public const string TournamentCompleted = "Tournament Completed.";
        public const string TournamentStarted = "Tournament Started.";
        public const string UnableAccessCurrentTournament = "Unable to get access to the current tournament. Try again later.";

        public static string AddListCommaSeparatedTeams(int teamsCount)
        {
            return $"Add a list of comma-separated team names. For multiple brackets, put the same teams on its own line. If the team name has a comma, use another comma to escape it (like ,,). You can add a maximum of {teamsCount} teams.";
        }

        public static string AllTournamentChannelsRolesRemoved(string tournamentName)
        {
            return $"All tournament channels and roles removed. Tournament '{tournamentName}' is now finished.";
        }

        public static string CannotMoveTournamentFromPending(string tournamentName)
        {
            return $"The tournament '{tournamentName}' couldn't be moved from pending to current. Try again later.";
        }

        public static string MustHaveMoreThanOneTeam(int teamCount)
        {
            return $"Must have more than 1 team. Count: {teamCount}.";
        }

        public static string NotEnoughReadersForTeams(int readersCount, int teamsCount)
        {
            return $"Not enough readers ({readersCount}) for the given number of teams ({teamsCount}).";
        }

        public static string OnlyAtMostNBrackets(int bracketsCount)
        {
            return $"There can only be at most {bracketsCount} brackets.";
        }

        public static string RoundRobinsMustBePositive(int roundRobinsCount)
        {
            return $"roundRobins must be positive. Value: {roundRobinsCount}.";
        }

        public static string SpecifyNumberRoundRobins(int maxRoundRobinCount)
        {
            return $"Specify the number of round-robin rounds as an integer (from 1 to {maxRoundRobinCount}).";
        }

        public static string TournamentAlreadyRunning(string tournamentName)
        {
            return $"The tournament '{tournamentName}' is already running. Use !end to stop it.";
        }

        public static string TournamentCannotBeFound(string tournamentName)
        {
            return $"A tournament with the name '{tournamentName}' cannot be found.";
        }

        public static string TournamentStartedDirections(string tournamentName)
        {
            return $"Tournament '{tournamentName}' has started. Go to your first round room and follow the instructions.";
        }
    }
}
