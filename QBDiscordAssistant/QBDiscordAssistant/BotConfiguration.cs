using Serilog.Events;

namespace QBDiscordAssistant
{
    public class BotConfiguration
    {
        public string BotToken { get; set; }

        public string MainChannelName { get; set; }

        public LogEventLevel LogEventLevel { get; set; }
    }
}
