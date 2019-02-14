using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    [RequireOwner]
    [RequireUserPermission(GuildPermission.Administrator)]
    public class AdminBotCommands : BotCommandsBase
    {
        public AdminBotCommands(GlobalTournamentsManager globalManager)
            : base(globalManager)
        {
        }

        private BotCommandHandler CommandHandler { get; }

        [Command("addTD")]
        [Summary("Adds a tournament director to a tournament, and creates that tournament if it doesn't exist yet.")]
        public Task AddTournamentDirector(
            [Summary("Member to add as the tournament director (as a @mention).")] IGuildUser newDirector,
            [Remainder] [Summary("Name of the tournament.")] string tournamentName)
        {
            return this.HandleCommand(commandHandler => commandHandler.AddTournamentDirector(newDirector, tournamentName));
        }

        // TODO: Allow other TDs to do this
        [Command("removeTD")]
        [Summary("Removes a tournament director from a tournament.")]
        public Task RemoveTournamentDirector(
            [Summary("Member to add as the tournament director (as a @mention).")] IGuildUser newDirector,
            [Summary("Name of the tournament.")] string tournamentName)
        {
            return this.HandleCommand(commandHandler => commandHandler.RemoveTournamentDirector(
                newDirector, tournamentName));
        }

        [Command("clearAll")]
        [Summary("Clears all leftover channels and roles from a tournament that didn't end cleanly.")]
        public Task ClearAll()
        {
            return this.HandleCommand(commandHandler => commandHandler.ClearAll());
        }
    }
}
