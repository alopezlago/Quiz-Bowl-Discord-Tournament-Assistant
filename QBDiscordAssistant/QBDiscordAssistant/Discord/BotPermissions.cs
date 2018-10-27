﻿using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Discord
{
    public class BotPermissions
    {
        public BotPermissions()
        {
            this.PossibleDirectors = new Dictionary<string, ISet<Director>>(StringComparer.CurrentCultureIgnoreCase);
            this.AdminIds = new List<ulong>();
        }

        // Maps tournament name to its directors
        // TODO: Look into using ISet<ulong>, since Director doesn't have anything else right now.
        public IDictionary<string, ISet<Director>> PossibleDirectors { get; private set; }
        
        public IList<ulong> AdminIds { get; private set; }
    }
}
