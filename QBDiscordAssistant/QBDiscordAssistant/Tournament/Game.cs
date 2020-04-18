using System;
using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public class Game
    {
        public Reader Reader { get; set; }

        public IEnumerable<Team> Teams { get; set; }

        public override string ToString()
        {
            IEnumerable<Team> teams = this.Teams ?? Array.Empty<Team>();
            return $"Game between teams {string.Join(",", teams)} read by {this.Reader}";
        }
    }
}
