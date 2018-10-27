﻿using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class Reader
    {
        public ulong Id { get; set; }

        public Room Room { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Reader otherReader)
            {
                return this.Id == otherReader.Id;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Id.GetHashCode();
        }
    }
}
