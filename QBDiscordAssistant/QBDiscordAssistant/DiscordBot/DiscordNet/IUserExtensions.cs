using Discord;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public static class IUserExtensions
    {
        public static bool IsAdmin(this IGuildUser user)
        {
            return user != null && user is IGuildUser guildUser && 
                (guildUser.Guild.OwnerId == user.Id || guildUser.GuildPermissions.Administrator);
        }
    }
}
