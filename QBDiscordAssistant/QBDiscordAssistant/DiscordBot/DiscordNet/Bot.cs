using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class Bot : IDisposable
    {
        private readonly IServiceCollection _map = new ServiceCollection();
        private readonly CommandService _commands = new CommandService();

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

        private IServiceProvider ServiceProvider { get; }

        public async Task ConnectAsync()
        {
            string token = this.Configuration.BotToken;
            this.Client.Log += this.Log;

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
                this.Client.Log -= this.Log;
                this.Client.MessageReceived -= this.OnMessageReceived;
                this.Client.Dispose();
                this.Client = null;
            }
        }

        private Task Log(LogMessage message)
        {
            Console.WriteLine($"{DateTime.UtcNow}: Severity {message.Severity.ToString()}: {message.Message}");
            if (message.Exception != null)
            {
                Console.WriteLine($"Exception: {message.Exception.Message}\n{message.Exception.StackTrace}");
            }

            return Task.CompletedTask;
        }

        private Task OnMessageReceived(SocketMessage message)
        {
            if (message.Author.Id == this.Client.CurrentUser.Id || !(message is IUserMessage userMessage))
            {
                return Task.CompletedTask;
            }

            int argPosition = 0;
            if (!userMessage.HasCharPrefix('!', ref argPosition))
            {
                // Do nothing for now.
                return Task.CompletedTask;
            }

            ICommandContext context = new CommandContext(this.Client, userMessage);
            return this.CommandService.ExecuteAsync(context, argPosition, this.ServiceProvider);
        }
    }
}
