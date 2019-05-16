using Discord.Commands;
using Serilog;
using System;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    [RequireContext(ContextType.Guild)]
    public abstract class BotCommandsBase : ModuleBase
    {
        public BotCommandsBase(GlobalTournamentsManager globalManager)
        {
            this.GlobalManager = globalManager;
            this.Logger = Log.ForContext(this.GetType());
        }

        protected GlobalTournamentsManager GlobalManager { get; }

        protected ILogger Logger { get; }

        protected Task HandleCommand(Func<BotCommandHandler, Task> handleCommandFunction)
        {
            // Discord.Net complains if a task takes too long while handling the command. Unfortunately, the current
            // tournament lock may block certain commands, and other commands are just long-running (like !start).
            // To work around this (and to keep the command handler unblocked), we have to run the task in a separate
            // thread, which requires us running it through Task.Run.
            BotCommandHandler commandHandler = new BotCommandHandler(this.Context, this.GlobalManager);
            Task.Run(async () => await handleCommandFunction(commandHandler));

            // If we return the task created by Task.Run the command handler will still be blocked. It seems like
            // Discord.Net will wait for the returned task to complete, which will block the Discord.Net's command
            // handler for too long. This does mean that we never know when a command is truly handled. This also
            // means that any data structures commands modify need to be thread-safe.
            return Task.CompletedTask;
        }
    }
}
