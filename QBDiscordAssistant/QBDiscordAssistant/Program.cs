using Newtonsoft.Json;
using QBDiscordAssistant.Discord;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBDiscordAssistant
{
    class Program
    {
        // Following the example from https://dsharpplus.emzi0767.com/articles/first_bot.html
        static void Main(string[] args)
        {
            MainAsync(args).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        static async Task MainAsync(string[] args)
        {

            BotConfiguration configuration = await GetConfiguration();
            using (Bot bot = new Bot(configuration))
            {
                await bot.ConnectAsync();

                // Never leave.
                await Task.Delay(-1);
            }
        }

        static async Task<BotConfiguration> GetConfiguration()
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
                    MainChannelName = "general"
                };
            }
        }
    }
}
