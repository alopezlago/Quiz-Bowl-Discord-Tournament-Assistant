﻿using System;
using System.Collections.Generic;
using System.Globalization;

namespace QBDiscordAssistant.Tournament
{
    public class TournamentState
    {
        // TODO: Move away from publically exposing collection methods.
        public TournamentState()
        {
            this.DirectorIds = new HashSet<ulong>();
            this.TeamRoleIds = new Dictionary<Team, ulong>();
            this.Players = new HashSet<Player>();
            this.Teams = new HashSet<Team>();
            this.Readers = new HashSet<Reader>();
            this.SymbolToTeam = new Dictionary<string, Team>();
            this.JoinTeamMessageIds = new HashSet<ulong>();
        }

        // Name acts as an ID
        public string Name { get; set; }

        // TODO: Need to gate commands and events based on this. Otherwise tournament state can be modified on other
        // servers.
        public ulong GuildId { get; set; }

        // TODO: Group this in a class (maybe TournamentRoleIds)
        public ulong DirectorRoleId { get; set; }

        public ulong[] ReaderRoomRoleIds { get; set; }

        public Dictionary<Team, ulong> TeamRoleIds { get; set; }

        public ISet<ulong> DirectorIds { get; private set; }

        public TournamentStage Stage { get; set; }

        public ISet<Player> Players { get; private set; }

        public ISet<Team> Teams { get; private set; }

        public ISet<Reader> Readers { get; private set; }

        public IDictionary<string, Team> SymbolToTeam { get; private set; }

        public ISet<ulong> JoinTeamMessageIds { get; private set; }

        // This should influence Schedule... leaving it here for now, but maybe it should moved to Schedule
        public int RoundRobinsCount { get; set; }

        // TODO: Make ISchedule?
        public Schedule Schedule { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is TournamentState otherReader)
            {
                return this.Name.Equals(otherReader.Name, StringComparison.CurrentCultureIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Name.ToLower(CultureInfo.CurrentCulture).GetHashCode();
        }
    }
}
