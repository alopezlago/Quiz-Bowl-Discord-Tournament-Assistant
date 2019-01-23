using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public class TournamentRoleIds
    {
        public TournamentRoleIds(
            ulong directorRoleId, ulong[] readerRoomRoleIds, IEnumerable<KeyValuePair<Team, ulong>> teamRoleIds)
        {
            this.DirectorRoleId = directorRoleId;
            this.ReaderRoomRoleIds = readerRoomRoleIds;
            this.TeamRoleIds = teamRoleIds;
        }

        public ulong DirectorRoleId { get; set; }

        public ulong[] ReaderRoomRoleIds { get; set; }

        public IEnumerable<KeyValuePair<Team, ulong>> TeamRoleIds { get; set; }
    }
}
