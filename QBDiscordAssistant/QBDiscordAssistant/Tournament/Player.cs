using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Player
    {
        public ulong Id { get; set; }

        public Team Team { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Player otherPlayer)
            {
                return this.Id == otherPlayer.Id;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
