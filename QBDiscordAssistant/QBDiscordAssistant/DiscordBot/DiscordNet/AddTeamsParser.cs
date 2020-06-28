using System.Collections.Generic;
using System.Linq;
using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public static class TeamsParser
    {
        public static bool TryParseTeams(string message, out IEnumerable<Team> teams, out string errorMessage)
        {
            Verify.IsNotNull(message, nameof(message));

            string[] teamsList = message.Split("\n");
            List<Team> allTeams = new List<Team>();
            for (int i = 0; i < teamsList.Length; i++)
            {
                string teamList = teamsList[i];
                if (!TeamNameParser.TryGetTeamNamesFromParts(
                    teamList, out IList<string> newTeamNames, out errorMessage))
                {
                    teams = null;
                    return false;
                }

                allTeams.AddRange(newTeamNames.Select(teamName =>
                    new Team()
                    {
                        Name = teamName,
                        Bracket = i
                    }));
            }

            errorMessage = null;
            teams = allTeams;
            return true;
        }
    }
}
