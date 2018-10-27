﻿using System;
using System.Collections.Generic;
using System.Text;

namespace QBDiscordAssistant.Tournament
{
    public class TournamentState
    {
        public TournamentState()
        {
            this.Directors = new Director[0];
            this.Players = new HashSet<Player>();
            this.Teams = new HashSet<Team>();
            this.Readers = new HashSet<Reader>();
        }

        // Name acts as an ID
        public string Name { get; set; }

        public ulong GuildId { get; set; }

        public Director[] Directors { get; set; }

        public TournamentStage Stage { get; set; }

        public ISet<Player> Players { get; private set; }

        public ISet<Team> Teams { get; private set; }

        public ISet<Reader> Readers { get; private set; }

        // TODO: Make ISchedule?
        public Schedule Schedule { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is TournamentState otherReader)
            {
                return this.Name == otherReader.Name;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Name.GetHashCode();
        }
    }
}
