using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Team
    {
        public string Name { get; set; }

        public ulong Id { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Team otherTeam)
            {
                return this.Id == otherTeam.Id;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
