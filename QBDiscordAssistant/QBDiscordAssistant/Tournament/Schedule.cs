using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Schedule
    {
        public Schedule()
        {
            this.Rounds = new List<Round>();
        }

        public IList<Round> Rounds { get; private set; }

        public void AddRound(Round round)
        {
            this.Rounds.Add(round);
        }

        public override string ToString()
        {
            return $"Scheduled games:\n{string.Join("\n", this.Rounds)}";
        }
    }
}
