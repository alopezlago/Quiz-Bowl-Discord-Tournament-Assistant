using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;

namespace QBDiscordAssistant
{
    internal class Program
    {
        // 200 MB file limit
        private const long maxLogfileSize = 1024 * 1024 * 200;

        // Following the example from https://dsharpplus.emzi0767.com/articles/first_bot.html
        public static void Main()
        {
            MainAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static async Task MainAsync()
        {
            LoggerConfiguration loggerConfiguration = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(
                    @"logs\bot.log",
                    fileSizeLimitBytes: maxLogfileSize,
                    retainedFileCountLimit: 10);

            BotConfiguration configuration;
            try
            {
                configuration = await GetConfiguration();
            }
            catch (Exception ex)
            {
                Log.Logger = loggerConfiguration
                    .MinimumLevel.Debug()
                    .CreateLogger();
                Log.Error(ex, "Failed to read configuration.");
                throw;
            }

            Log.Logger = loggerConfiguration
                .MinimumLevel.Is(configuration.LogEventLevel)
                .CreateLogger();
            Log.Information("Bot started with log event level {0}", configuration.LogEventLevel);

            using (DiscordBot.DiscordNet.Bot bot = new DiscordBot.DiscordNet.Bot(configuration))
            {
                await bot.ConnectAsync();

                // Never leave.
                await Task.Delay(-1);
            }

            Log.Information("Bot escaped infinite delay. Should investigate");
        }

        private static async Task<BotConfiguration> GetConfiguration()
        {
            // TODO: Get the token from an encrypted file. This could be done by using DPAPI and writing a tool to help
            // convert the user access token into a token file using DPAPI. The additional entropy could be a config
            // option.
            // In preparation for this work the token is still taken from a separate file.
            string botToken = await File.ReadAllTextAsync("discordToken.txt");

            if (File.Exists("config.txt"))
            {
                string jsonOptions = await File.ReadAllTextAsync("config.txt");
                BotConfiguration configuration = JsonConvert.DeserializeObject<BotConfiguration>(jsonOptions);
                configuration.BotToken = botToken;
                return configuration;
            }
            else
            {
                return new BotConfiguration()
                {
                    BotToken = botToken,
                    MainChannelName = "general",
                    LogEventLevel = LogEventLevel.Debug
                };
            }
        }
    }
}
