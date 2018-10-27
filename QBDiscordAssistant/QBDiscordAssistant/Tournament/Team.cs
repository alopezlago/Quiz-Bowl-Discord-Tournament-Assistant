using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Team
    {
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Team otherTeam)
            {
                return this.Name == otherTeam.Name;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }
}
