using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public static class BotStrings
    {
        public const string AllReaderNamesShouldBeUnique = "All reader names should be unique.";
        public const string CommandOnlyUsedTournamentReadyStart = "This command can only be used when a tournament is ready to start, which is after all the players have joined their teams.";
        public const string CommandOnlyUsedWhileTournamentRunning = "This command can only be used while the tournament is running. Use !back if you are still setting up the tournament.";
        public static string CouldntGetRoleForTheOldReader = "Couldn't get the role for the old reader. Readers were not switched. You may need to manually switch the roles.";
        public const string CreatingChannelsAndRoles = "Creating the channels and roles...";
        public const string ErrorFinalsOnlySetDuringPrelims = "Error: finals can only be set during the prelims.";
        public const string ErrorGivenUserIsntAReader = "Error: given user isn't a reader.";
        public const string ErrorNoTeamsSpecified = "Error: No teams specified.";
        public const string NoTeamsYet = " No teams have been created yet.";
        public const string ReadersSwitchedSuccessfully = "Readers switched successfully.";
        public const string TournamentWasNotRemoved = "Tournament was not removed from the list of pending tournaments. Try the command again.";
        public const string UnknownErrorRemovingOldReader = "Unknown error when trying to remove the old reader.";

        public static string AddPlayerSuccessful(string playerName, string teamName)
        {
            return $"Player {playerName} added to team '{teamName}'.";
        }

        public static string AddTournamentDirectorSuccessful(string tournamentName, string guildName)
        {
            return $"Added tournament director to tournament '{tournamentName}' in guild '{guildName}'.";
        }

        public static string AllPossibleTournamentArtifactsCleaned(string guildName)
        {
            return $"All possible tournament artifacts cleaned up in guild '{guildName}'.";
        }
       
        public static string CannotGoBack(TournamentStage stage)
        {
            return $"Cannot go back from the stage {stage}.";
        }

        public static string CurrentTournamentInGuild(string guildName, string tournamentName)
        {
            return $"Current tournament in guild '{guildName}': {tournamentName}.";
        }

        public static string ErrorAtLeastOneTeamNotInTournament(string teamNames)
        {
            return $"Error: At least one team specified is not in the tournament. Teams specified: {teamNames}.";
        }

        public static string ErrorGenericMessage(string message)
        {
            return $"Error: {message}.";
        }

        public static string ErrorSettingCurrentTournament(string guildName, string errorMessage)
        {
            return $"Error setting the current tournament in guild '{guildName}'. {errorMessage}.";
        }

        public static string ErrorTwoTeamsMustBeSpecifiedFinals(int teamCount)
        {
            return $"Error: two teams must be specified in the finals. You have specified {teamCount}.";
        }

        public static string FinalsParticipantsPleaseJoin(string room)
        {
            return $"Finals participants: please join the room {room} and join the voice channel for that room number.";
        }

        public static string IsAlreadyReader(string name)
        {
            return $"{name} is already a reader. The new reader must not be an existing reader.";
        }

        public static string NotACurrentReader(string name)
        {
            return $"{name} is not a current reader. You can only replace existing readers.";
        }

        public static string PlayerIsAlreadyOnTeam(string name)
        {
            return $"Player '{name}' is already on a team.";
        }

        public static string PlayerIsNotOnAnyTeam(string name)
        {
            return $"Player '{name}' is not on any team.";
        }

        public static string PlayerRemoved(string name)
        {
            return $"Player {name} removed.";
        }

        public static string RemovedTournamentDirector(string tournamentName, string guildName)
        {
            return $"Removed tournament director from tournament '{tournamentName}' in guild '{guildName}'.";
        }

        public static string TeamDoesNotExist(string teamName)
        {
            return $"Team '{teamName}' does not exist.";
        }

        public static string TournamentCleanupFinished(string guildName)
        {
            return $"Tournament cleanup finished in guild '{guildName}'.";
        }

        public static string TournamentDoesNotExist(string tournamentName, string guildName)
        {
            return $"Tournament '{tournamentName}' does not exist in guild '{guildName}'.";
        }

        public static string TournamentHasStarted(string tournamentName)
        {
            return $"{tournamentName}: tournament has started.";
        }

        public static string UnableToPerformCommand(string message)
        {
            return $"Unable to perform command. {message}";
        }

        public static string UserAlreadyTournamentDirector(string tournamentName, string guildName)
        {
            return $"User is already a director of '{tournamentName}' in guild '{guildName}'.";
        }

        public static string UserNotTournamentDirector(string tournamentName, string guildName)
        {
            return $"User is not a director for tournament '{tournamentName}' in guild '{guildName}', or user was just removed.";
        }
    }
}
