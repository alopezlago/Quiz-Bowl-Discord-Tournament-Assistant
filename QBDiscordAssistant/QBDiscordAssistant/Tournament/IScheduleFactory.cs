using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public interface IScheduleFactory
    {
        Schedule Generate(ISet<Team> teams, ISet<Reader> readers);
    }
}
