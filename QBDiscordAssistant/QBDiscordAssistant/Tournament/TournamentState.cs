using System;
using System.Collections.Generic;
using System.Globalization;

namespace QBDiscordAssistant.Tournament
{
    public class TournamentState
    {
        // TODO: see if there's a better place for this
        public const int MaxRoundRobins = 5;

        private HashSet<ulong> directorIds;
        private Dictionary<ulong, Player> players;
        private Dictionary<ulong, Reader> readers;
        private Dictionary<string, Team> teams;

        public TournamentState()
        {
            this.directorIds = new HashSet<ulong>();
            this.players = new Dictionary<ulong, Player>();
            this.teams = new Dictionary<string, Team>();
            this.readers = new Dictionary<ulong, Reader>();
            this.SymbolToTeam = new Dictionary<string, Team>();
            this.TeamRoleIds = new Dictionary<Team, ulong>();
            this.JoinTeamMessageIds = new HashSet<ulong>();
        }

        // TODO: State is too spread out, is unsynchronized while being used in async methods, and collections  are too
        // accessible. We need to do the following:
        // - Hide the collections behind methods; we don't usually need direct access to them, we just need to do these
        //   3 ops: add, remove, contains. Sometimes we need to Select/project.
        //   - We should hide the Discord-specific state in a separate change because we might move them behind another
        //     class, and they don't have the same synchronization issue.
        // - Lock access to the collections, especially DirectorIds and Players since they're not done in one shot. We
        //   may want to look ininto Concurrent versions of the collections.
        //   - Highest priority is DirectorIds and Players since those are more easily contested, but we could have
        //     multiple TDs setting up the tournament, so we should lock the others (and reject those which are late)
        // - Move the Discord-specific state (GuildId, XRoleIds, SymbolToTeam, JoinTeamMessageIds) to their own classes
        //   - Need to consider whether to split this into two pieces (TournamentState and DiscordState), into two
        //     separate classes under this one, or to keep the fields here and add DiscordState as another one.
        //   - This might just be organizing for the sake of organizing: it might make sense to split it once we start
        //     doing more automation (i.e. passing tournament-specific state to those classes instead of including 
        //     unrelated Discord stuff), but we may want to wait until we need those changes.
        //      - The one benefit might be to make the Discord class an interface, and have this implementation take
        //        a client, so that it can create the roles on itself, and we just need to call a method to initialize
        //        the roles (and get access to the role or role IDs).

        // Name acts as an ID
        public string Name { get; set; }

        // TODO: Need to gate commands and events based on this. Otherwise tournament state can be modified on other
        // servers.
        // TODO: This should arguably be a readonly field that's passed into the constructor. We could have a
        // TournamentStateBuilder that adds Name/GuildId and then creates this class. Alternatively, we keep the builder
        // around until we're ready to start the tournament.
        public ulong GuildId { get; set; }

        // TODO: Group this in a class (maybe TournamentRoleIds)
        public ulong DirectorRoleId { get; set; }

        public ulong[] ReaderRoomRoleIds { get; set; }

        public Dictionary<Team, ulong> TeamRoleIds { get; set; }

        public IEnumerable<ulong> DirectorIds => this.directorIds;

        // TODO: We should hide this value behind a lock.
        public TournamentStage Stage { get; private set; }

        public IEnumerable<Player> Players => this.players.Values;

        public IEnumerable<Team> Teams => this.teams.Values;

        public IEnumerable<Reader> Readers => this.readers.Values;

        public IDictionary<string, Team> SymbolToTeam { get; private set; }

        public ISet<ulong> JoinTeamMessageIds { get; private set; }

        // This should influence Schedule... leaving it here for now, but maybe it should moved to Schedule
        public int RoundRobinsCount { get; set; }

        // TODO: Make ISchedule?
        public Schedule Schedule { get; set; }

        public bool TryAddDirector(ulong directorId)
        {
            return this.directorIds.Add(directorId);
        }

        public bool TryRemoveDirector(ulong directorId)
        {
            return this.directorIds.Remove(directorId);
        }

        public bool TryRemoveReader(ulong readerId)
        {
            return this.readers.Remove(readerId);
        }

        public bool IsDirector(ulong directorId)
        {
            return this.directorIds.Contains(directorId);
        }

        /// <summary>
        /// Adds a player if they exist.
        /// </summary>
        /// <param name="player">Player to add to the tournament</param>
        /// <returns>true if the player was added to the tournament. false if the player is already in the tournament.</returns>
        public bool TryAddPlayer(Player player)
        {
            if (this.players.ContainsKey(player.Id))
            {
                return false;
            }

            this.players[player.Id] = player;
            return true;
        }

        public bool TryRemovePlayer(ulong playerId)
        {
            return this.players.Remove(playerId);
        }

        public bool TryGetPlayerTeam(ulong playerId, out Team team)
        {
            team = null;
            if (this.players.TryGetValue(playerId, out Player player))
            {
                return false;
            }

            team = player.Team;
            return true;
        }

        public bool TryAddReaders(IEnumerable<Reader> readers)
        {
            foreach (Reader reader in readers)
            {
                this.readers[reader.Id] = reader;
            }

            return true;
        }

        public bool TryClearReaders()
        {
            this.readers.Clear();
            return true;
        }

        public bool TryGetReader(ulong readerId, out Reader reader)
        {
            return this.readers.TryGetValue(readerId, out reader);
        }

        public bool IsReader(ulong readerId)
        {
            return this.readers.ContainsKey(readerId);
        }

        public bool TryAddTeams(IEnumerable<Team> teams)
        {
            foreach (Team team in teams)
            {
                this.teams[team.Name] = team;
            }

            return true;
        }

        public bool TryClearTeams()
        {
            this.teams.Clear();
            return true;
        }

        public bool TryGetTeam(string teamName, out Team team)
        {
            return this.teams.TryGetValue(teamName, out team);
        }

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
            return this.readers.Count * 2 + 1;
        }
    }
}
