using System;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public sealed class Bot : IDisposable
    {
        public Bot(BotConfiguration configuration)
        {
            DiscordSocketConfig clientConfig = new DiscordSocketConfig()
            {
                // 16 kb?
                MessageCacheSize = 1024 * 16
            };
            this.Client = new DiscordSocketClient(clientConfig);
            this.Configuration = configuration;
            this.CommandService = new CommandService(new CommandServiceConfig()
            {
                CaseSensitiveCommands = false,
                LogLevel = LogSeverity.Info
            });

            this.Logger = Log.ForContext(this.GetType());

            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            IServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(this.Client);
            serviceCollection.AddSingleton(globalManager);
            serviceCollection.AddSingleton(this.Configuration);
            this.ServiceProvider = serviceCollection.BuildServiceProvider();

            this.EventHandler = new BotEventHandler(this.Client, globalManager);

            // TODO: If we want to split up BotCommands, use Assembly.GetEntryAssembly.
            Task.WaitAll(this.CommandService.AddModulesAsync(Assembly.GetExecutingAssembly(), this.ServiceProvider));
        }

        private BotEventHandler EventHandler { get; set; }

        // TODO: See if we can make this generic. LoginAsync doens't work with IDiscordClient.
        private DiscordSocketClient Client { get; set; }

        private CommandService CommandService { get; }

        private BotConfiguration Configuration { get; }

        private ILogger Logger { get; }

        private IServiceProvider ServiceProvider { get; }

        public async Task ConnectAsync()
        {
            string token = this.Configuration.BotToken;
            this.Client.Log += this.LogMessageAsync;

            // TODO: move this code to BotEventHandler. This does require rewriting a fair amount of the event handlers,
            // since we need to use a socket client in it.
            this.Client.MessageReceived += this.OnMessageReceived;
            await this.Client.LoginAsync(TokenType.Bot, token);
            await this.Client.StartAsync();
        }

        public void Dispose()
        {
            if (this.EventHandler != null)
            {
                this.EventHandler.Dispose();
                this.EventHandler = null;
            }

            if (this.Client != null)
            {
                this.Client.Log -= this.LogMessageAsync;
                this.Client.MessageReceived -= this.OnMessageReceived;
                this.Client.Dispose();
                this.Client = null;
            }
        }

        private static LogEventLevel ConvertLogLevels(LogSeverity discordSeverity)
        {
            switch (discordSeverity)
            {
                case LogSeverity.Critical:
                    return LogEventLevel.Fatal;
                case LogSeverity.Error:
                    return LogEventLevel.Error;
                case LogSeverity.Warning:
                    return LogEventLevel.Warning;
                case LogSeverity.Info:
                    return LogEventLevel.Information;
                case LogSeverity.Verbose:
                    // Verbose and Debug are swapped between the two levels. Verbose is for debug level events
                    return LogEventLevel.Debug;
                case LogSeverity.Debug:
                    return LogEventLevel.Verbose;
                default:
                    throw new ArgumentOutOfRangeException(nameof(discordSeverity));
            }
        }

        private Task LogMessageAsync(LogMessage message)
        {
            LogEventLevel logLevel = ConvertLogLevels(message.Severity);
            this.Logger.Write(logLevel, "Discord.Net message: {0}", message.Message);
            if (message.Exception != null)
            {
                this.Logger.Write(logLevel, message.Exception, "Exception occurred on Discord.Net side");
            }

            return Task.CompletedTask;
        }

        private async Task OnMessageReceived(SocketMessage message)
        {
            if (message.Author.Id == this.Client.CurrentUser.Id || !(message is IUserMessage userMessage))
            {
                return;
            }

            int argPosition = 0;
            if (!userMessage.HasCharPrefix('!', ref argPosition))
            {
                // Do nothing for now.
                return;
            }

            ICommandContext context = new CommandContext(this.Client, userMessage);
            IResult result = await this.CommandService.ExecuteAsync(context, argPosition, this.ServiceProvider);
            if (result.Error == CommandError.Exception && result is ExecuteResult executeResult)
            {
                this.Logger.Error(executeResult.Exception, "Exception occurred in comand handler");
            }
        }
    }
}
