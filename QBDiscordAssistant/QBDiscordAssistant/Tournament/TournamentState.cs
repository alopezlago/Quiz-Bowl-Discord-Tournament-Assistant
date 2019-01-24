using System;
using System.Collections.Generic;
using System.Globalization;

namespace QBDiscordAssistant.Tournament
{
    public class TournamentState : ITournamentState
    {
        // TODO: see if there's a better place for this
        public const int MaxRoundRobins = 5;

        private readonly HashSet<ulong> directorIds;
        private readonly Dictionary<ulong, Player> players;
        private readonly Dictionary<ulong, Reader> readers;
        private readonly Dictionary<string, Team> teams;

        public TournamentState(ulong guildId, string name)
        {
            this.directorIds = new HashSet<ulong>();
            this.players = new Dictionary<ulong, Player>();
            this.teams = new Dictionary<string, Team>();
            this.readers = new Dictionary<ulong, Reader>();

            this.GuildId = guildId;
            this.Name = name;
            this.AddPlayersState = new AddPlayersState();
        }

        // Name acts as an ID
        public string Name { get; }

        public ulong GuildId { get; }

        public TournamentRoleIds TournamentRoles { get; set; }

        public IEnumerable<ulong> DirectorIds => this.directorIds;

        public TournamentStage Stage { get; private set; }

        public IEnumerable<Player> Players => this.players.Values;

        public IEnumerable<Team> Teams => this.teams.Values;

        public IEnumerable<Reader> Readers => this.readers.Values;

        public IEnumerable<ulong> JoinTeamMessageIds => this.AddPlayersState.JoinTeamMessageIds;

        // This should influence Schedule... leaving it here for now, but maybe it should moved to Schedule
        public int RoundRobinsCount { get; set; }

        private AddPlayersState AddPlayersState { get; }

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

        public void AddJoinTeamMessageId(ulong messageId)
        {
            this.AddPlayersState.JoinTeamMessageIds.Add(messageId);
        }

        public void ClearJoinTeamMessageIds()
        {
            this.AddPlayersState.JoinTeamMessageIds.Clear();
        }

        public bool IsJoinTeamMessage(ulong messageId)
        {
            return this.AddPlayersState.JoinTeamMessageIds.Contains(messageId);
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
            if (!this.players.TryGetValue(playerId, out Player player))
            {
                return false;
            }

            team = player.Team;
            return true;
        }

        public void AddReaders(IEnumerable<Reader> readers)
        {
            foreach (Reader reader in readers)
            {
                this.readers[reader.Id] = reader;
            }
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

        public void AddSymbolToTeam(string symbol, Team team)
        {
            this.AddPlayersState.SymbolToTeam.Add(symbol, team);
        }

        public void ClearSymbolsToTeam()
        {
            this.AddPlayersState.SymbolToTeam.Clear();
        }

        public bool TryGetTeamFromSymbol(string symbol, out Team team)
        {
            return this.AddPlayersState.SymbolToTeam.TryGetValue(symbol, out team);
        }

        public void AddTeams(IEnumerable<Team> teams)
        {
            foreach (Team team in teams)
            {
                this.teams[team.Name] = team;
            }
        }

        public void RemoveTeams(IEnumerable<Team> teams)
        {
            foreach (Team team in teams)
            {
                this.teams.Remove(team.Name);
            }
        }

        public bool TryClearTeams()
        {
            this.teams.Clear();
            return true;
        }

        public bool TryGetTeamFromName(string teamName, out Team team)
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
