using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Director
    {
        public ulong Id { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Director otherDirector)
            {
                return this.Id == otherDirector.Id;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
