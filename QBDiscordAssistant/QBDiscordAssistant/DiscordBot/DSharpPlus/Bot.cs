using DSharpPlus;
using DSharpPlus.CommandsNext;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DSharpPlus
{
    public class Bot : IDisposable
    {
        private readonly BotConfiguration options;
        private readonly DiscordClient discordClient;
        private readonly CommandsNextModule commandsModule;
        private readonly BotEventHandler eventHandler;

        private bool isDisposed = false;

        public Bot(BotConfiguration configuration)
        {
            this.options = configuration;

            this.discordClient = new DiscordClient(new DiscordConfiguration()
            {
                Token = configuration.BotToken,
                TokenType = TokenType.Bot,
                UseInternalLogHandler = true,
                LogLevel = LogLevel.Debug
            });

            DependencyCollectionBuilder dependencyCollectionBuilder = new DependencyCollectionBuilder();
            dependencyCollectionBuilder.AddInstance(configuration);

            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            dependencyCollectionBuilder.AddInstance(globalManager);

            this.commandsModule = this.discordClient.UseCommandsNext(new CommandsNextConfiguration()
            {
                StringPrefix = "!",
                CaseSensitive = false,
                EnableDms = false,
                Dependencies = dependencyCollectionBuilder.Build()
            });

            this.commandsModule.RegisterCommands<BotCommands>();

            this.eventHandler = new BotEventHandler(this.discordClient, globalManager);
        }

        public Task ConnectAsync()
        {
            return this.discordClient.ConnectAsync();
        }

        // This likely won't be called, but the client is IDisposable so we should do our due diligence.
        public void Dispose()
        {
            if (!this.isDisposed)
            {
                this.eventHandler.Dispose();
                this.discordClient.Dispose();
                this.isDisposed = true;
            }
        }
    }
}
