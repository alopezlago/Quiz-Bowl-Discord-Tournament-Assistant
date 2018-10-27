using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public interface IScheduleFactory
    {
        Schedule Generate(IList<Team> teams, IList<Reader> readers);
    }
}
