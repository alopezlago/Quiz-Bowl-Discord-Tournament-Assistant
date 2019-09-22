using Discord;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public static class RequestOptionsSettings
    {
        public static RequestOptions Default => new RequestOptions()
        {
            RetryMode = RetryMode.AlwaysRetry
        };
    }
}
