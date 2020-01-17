using System.Threading.Tasks;
using Discord.Commands;

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
            this.Logger.Information("{0} asking for current tournament", this.Context.User.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.GetCurrentTournamentAsync());
        }
    }
}
