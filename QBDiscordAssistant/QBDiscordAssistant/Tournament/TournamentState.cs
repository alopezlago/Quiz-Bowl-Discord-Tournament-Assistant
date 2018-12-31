using System;
using System.Collections.Generic;
using System.Globalization;

namespace QBDiscordAssistant.Tournament
{
    public class TournamentState
    {
        // TODO: see if there's a better place for this
        public const int MaxRoundRobins = 5;

        // TODO: Move away from publically exposing collection methods.
        public TournamentState()
        {
            this.DirectorIds = new HashSet<ulong>();
            this.TeamRoleIds = new Dictionary<Team, ulong>();
            this.Players = new HashSet<Player>();
            this.Teams = new HashSet<Team>();
            this.Readers = new HashSet<Reader>();
            this.SymbolToTeam = new Dictionary<string, Team>();
            this.JoinTeamMessageIds = new HashSet<ulong>();
        }

        // Name acts as an ID
        public string Name { get; set; }

        // TODO: Need to gate commands and events based on this. Otherwise tournament state can be modified on other
        // servers.
        public ulong GuildId { get; set; }

        // TODO: Group this in a class (maybe TournamentRoleIds)
        public ulong DirectorRoleId { get; set; }

        public ulong[] ReaderRoomRoleIds { get; set; }

        public Dictionary<Team, ulong> TeamRoleIds { get; set; }

        public ISet<ulong> DirectorIds { get; private set; }

        public TournamentStage Stage { get; set; }

        public ISet<Player> Players { get; private set; }

        public ISet<Team> Teams { get; private set; }

        public ISet<Reader> Readers { get; private set; }

        public IDictionary<string, Team> SymbolToTeam { get; private set; }

        public ISet<ulong> JoinTeamMessageIds { get; private set; }

        // This should influence Schedule... leaving it here for now, but maybe it should moved to Schedule
        public int RoundRobinsCount { get; set; }

        // TODO: Make ISchedule?
        public Schedule Schedule { get; set; }

        public void UpdateStage(TournamentStage newStage, out string nextStageTitle, out string nextStageInstructions)
        {
            this.Stage = newStage;
            switch (newStage)
            {
                case TournamentStage.AddReaders:
                    nextStageTitle = "Add Readers";
                    nextStageInstructions = "List the mentions of all of the readers. For example, '@Reader_1 @Reader_2 @Reader_3'. If you forgot a reader, you can still use !addReaders during the add teams phase.";
                    break;
                case TournamentStage.SetRoundRobins:
                    nextStageTitle = "Set the number of round robins";
                    nextStageInstructions = $"Specify the number of round-robin rounds as an integer (from 1 to {MaxRoundRobins}).";
                    break;
                case TournamentStage.AddTeams:
                    nextStageTitle = "Add Teams";
                    nextStageInstructions =
                        $"Add a list of comma-separated team names. If the team name has a comma, use another comma to escape it (like ,,). You can add a maximum of {this.GetMaximumTeamCount()} teams.";
                    break;
                case TournamentStage.BotSetup:
                    nextStageTitle = "Setting up the tournament";
                    nextStageInstructions = "Initializing the schedule. Channels and roles will be set up next.";
                    break;
                case TournamentStage.RunningPrelims:
                    nextStageTitle = "Tournament Started";
                    nextStageInstructions = $"Tournament '{this.Name}' has started. Go to your first round room and follow the instructions.";
                    break;
                case TournamentStage.Complete:
                    nextStageTitle = "Tournament Completed";
                    nextStageInstructions = $"All tournament channels and roles removed. Tournament '{this.Name}' is now finished.";
                    break;
                default:
                    nextStageTitle = null;
                    nextStageInstructions = null;
                    break;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is TournamentState otherReader)
            {
                return this.Name.Equals(otherReader.Name, StringComparison.CurrentCultureIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Name.ToLower(CultureInfo.CurrentCulture).GetHashCode();
        }

        private int GetMaximumTeamCount()
        {
            return this.Readers.Count * 2 + 1;
        }
    }
}
