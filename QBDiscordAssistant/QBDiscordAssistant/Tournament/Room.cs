using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Room
    {
        // TODO: We may not need this if we can get the channels from the name. We may also just have a separate
        // Channel class which has the Room in it.
        public ulong Id { get; set; }

        public string Name { get; set; }

        public Reader Reader { get; set; }

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
