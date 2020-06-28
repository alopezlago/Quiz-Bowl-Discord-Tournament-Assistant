using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Discord;
using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class TournamentRoles
    {
        public IRole DirectorRole { get; set; }

        [SuppressMessage(
            "Usage", "CA2227:Collection properties should be read only", Justification = "Allows for easier initialization")]
        public IDictionary<Reader, IRole> RoomReaderRoles { get; set; }

        [SuppressMessage(
            "Usage", "CA2227:Collection properties should be read only", Justification = "Allows for easier initialization")]
        public Dictionary<Team, IRole> TeamRoles { get; set; }

        public TournamentRoleIds ToIds()
        {
            return new TournamentRoleIds(
                this.DirectorRole.Id,
                this.RoomReaderRoles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id),
                this.TeamRoles.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Id));
        }
    }
}
