using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    // We don't need the BotCommandBase infrastructure for this method, since this task should be completed quickly and
    // should be allowed in DMs.
    public class GeneralBotCommands : ModuleBase
    {
        private readonly CommandService commandService;

        public GeneralBotCommands(CommandService commandService)
        {
            this.commandService = commandService;
        }

        // TODO: Look into filtering out commands that the user doesn't have access to (e.g. check if they are a reader
        // or tournament director or admin.). We could use the commandInfo's Preconditions list to verify this.
        // Don't implement it until further consideration. I need to consider the tradeoffs from having a consistent
        // list of commands and showing only the commands usable by the current user.

        [Command("help")]
        [Summary("Lists available commands and how to use them.")]
        public Task Help()
        {
            return this.SendHelpInformation();
        }

        [Command("help")]
        [Summary("Lists available commands and how to use them.")]
        public Task Help([Remainder] [Summary("Command name")] string rawCommandName)
        {
            return this.SendHelpInformation(rawCommandName);
        }

        private Task SendHelpInformation(string rawCommandName = null)
        {
            EmbedBuilder embedBuilder = new EmbedBuilder();
            IEnumerable<CommandInfo> commands = this.commandService.Commands
                .Where(command => command.Name != "help");

            if (rawCommandName != null)
            {
                string commandName = rawCommandName.Trim();
                commands = commands
                    .Where(command => command.Name.Equals(commandName, StringComparison.CurrentCultureIgnoreCase));
            }

            foreach (CommandInfo commandInfo in commands)
            {
                string parameters = string.Join(' ', commandInfo.Parameters.Select(parameter => $"*{parameter.Name}*"));
                string name = $"{commandInfo.Name} {parameters}";
                embedBuilder.AddField(name, commandInfo.Summary ?? "<undocumented>");
            }

            // DM the user, so that we can't spam the channel.
            return this.Context.User.SendMessageAsync(embed: embedBuilder.Build());
        }
    }
}
