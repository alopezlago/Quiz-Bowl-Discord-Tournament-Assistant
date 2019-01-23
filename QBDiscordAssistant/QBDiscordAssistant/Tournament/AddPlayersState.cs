using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    internal class AddPlayersState
    {
        public AddPlayersState()
        {
            this.SymbolToTeam = new Dictionary<string, Team>();
            this.JoinTeamMessageIds = new HashSet<ulong>();
        }

        public IDictionary<string, Team> SymbolToTeam { get; }

        public ISet<ulong> JoinTeamMessageIds { get; }
    }
}
