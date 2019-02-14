using Discord.Commands;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class GuildBotCommands : BotCommandsBase
    {
        public GuildBotCommands(GlobalTournamentsManager globalManager)
            : base(globalManager)
        {
        }

        [Command("getCurrentTournament")]
        [Summary("Gets the name of the current tournament, if it exists.")]
        public Task GetCurrentTournament()
        {
            return this.HandleCommand(commandHandler => commandHandler.GetCurrentTournament());
        }
    }
}
