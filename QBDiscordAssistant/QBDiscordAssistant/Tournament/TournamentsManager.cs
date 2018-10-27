using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    // TODO: Find a way to persist this state on disk so that we can recover when the bot is closed/goes down.
    // If we want to do that we should take in an interface to persist this state, and add methods for adding/removing
    // tournaments.
    public class TournamentsManager
    {
        public TournamentsManager()
        {
            this.PendingTournaments = new Dictionary<string, TournamentState>();
        }

        public ulong GuildId { get; set; }

        // Maps the tournament name to the state
        public IDictionary<string, TournamentState> PendingTournaments { get; set; }

        public TournamentState CurrentTournament { get; set; }
    }
}
