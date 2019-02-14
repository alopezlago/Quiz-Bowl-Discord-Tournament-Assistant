using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Concurrent;

namespace QBDiscordAssistant
{
    // Maps between guilds and tournaments managers
    // Do we need to lock this too? Or we could use a ConcurrentDictionary.
    public class GlobalTournamentsManager
    {
        public GlobalTournamentsManager()
        {
            this.Managers = new ConcurrentDictionary<ulong, TournamentsManager>();
        }

        private ConcurrentDictionary<ulong, TournamentsManager> Managers { get; }

        public TournamentsManager GetOrAdd(ulong id, Func<ulong, TournamentsManager> createManager)
        {
            return this.Managers.GetOrAdd(id, createManager);
        }
    }
}
