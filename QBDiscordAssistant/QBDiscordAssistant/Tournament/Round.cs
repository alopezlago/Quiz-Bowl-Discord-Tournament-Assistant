using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public class Round
    {
        public Round()
        {
            this.Games = new List<Game>();
        }

        public IList<Game> Games { get; private set; }

        public override string ToString()
        {
            return $"Round games: {string.Join(";", this.Games)}";
        }
    }
}
