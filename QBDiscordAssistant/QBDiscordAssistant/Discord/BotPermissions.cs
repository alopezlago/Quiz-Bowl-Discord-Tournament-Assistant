using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Discord
{
    public class BotPermissions
    {
        public BotPermissions()
        {
            this.PossibleDirectors = new Dictionary<string, ISet<Director>>();
            this.AdminIds = new List<ulong>();
        }

        // Maps tournament name to its directors
        public IDictionary<string, ISet<Director>> PossibleDirectors { get; private set; }
        
        public IList<ulong> AdminIds { get; private set; }
    }
}
