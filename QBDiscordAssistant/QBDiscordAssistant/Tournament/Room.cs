using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Room
    {
        public ulong Id { get; set; }

        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Room otherRoom)
            {
                return this.Id == otherRoom.Id;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
