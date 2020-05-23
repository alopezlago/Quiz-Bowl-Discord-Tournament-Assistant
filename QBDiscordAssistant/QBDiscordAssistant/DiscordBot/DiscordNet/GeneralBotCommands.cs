using System.Threading.Tasks;
using Discord.Commands;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class GeneralBotCommands : BotCommandsBase
    {
        public GeneralBotCommands(GlobalTournamentsManager globalManager) 
            : base(globalManager)
        {
        }

        [Command("schedule")]
        [Summary("Gets the schedule for the current tournament")]
        public Task GetSchedule()
        {
            return this.HandleCommandAsync(commandHandler => commandHandler.GetScheduleAsync());
        }

        [Command("schedule")]
        [Summary("Gets the schedule for a team in the current tournament")]
        public Task GetSchedule(string teamName)
        {
            return this.HandleCommandAsync(commandHandler => commandHandler.GetScheduleAsync(teamName));
        }
    }
}
