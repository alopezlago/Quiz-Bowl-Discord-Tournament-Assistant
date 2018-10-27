using System;
using System.Globalization;

namespace QBDiscordAssistant.Tournament
{
    public class Team
    {
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is Team otherTeam)
            {
                return this.Name.Equals(otherTeam.Name, StringComparison.CurrentCultureIgnoreCase);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return this.Name.ToLower(CultureInfo.CurrentCulture).GetHashCode();
        }
    }
}
