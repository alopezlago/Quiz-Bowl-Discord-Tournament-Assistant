﻿using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    [RequireUserPermission(GuildPermission.Administrator)]
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "Dependency injection will fail before null value passed in")]
    public class AdminBotCommands : BotCommandsBase
    {
        public AdminBotCommands(GlobalTournamentsManager globalManager)
            : base(globalManager)
        {
        }

        [Command("addTD")]
        [Summary("Adds a tournament director to a tournament, and creates that tournament if it doesn't exist yet.")]
        public Task AddTournamentDirector(
            [Summary("Member to add as the tournament director (as a @mention).")] IGuildUser newDirector,
            [Remainder][Summary("Name of the tournament.")] string tournamentName)
        {
            this.Logger.Information(
                "{0} adding TD {1} for {2}", this.Context.User.Id, newDirector.Id, tournamentName);
            return this.HandleCommandAsync(commandHandler => commandHandler.AddTournamentDirectorAsync(newDirector, tournamentName));
        }

        // TODO: Allow other TDs to do this
        [Command("removeTD")]
        [Summary("Removes a tournament director from a tournament.")]
        public Task RemoveTournamentDirector(
            [Summary("Member to add as the tournament director (as a @mention).")] IGuildUser oldDirector,
            [Summary("Name of the tournament.")] string tournamentName)
        {
            this.Logger.Information(
                "{0} removing TD {1} for {2}", this.Context.User.Id, oldDirector.Id, tournamentName);
            return this.HandleCommandAsync(commandHandler => commandHandler.RemoveTournamentDirectorAsync(
                oldDirector, tournamentName));
        }

        [Command("clearAll")]
        [Summary("Clears all leftover channels and roles from a tournament that didn't end cleanly.")]
        public Task ClearAll()
        {
            this.Logger.Information(
                "{0} clearing everything in channel {1}", this.Context.User.Id, this.Context.Channel.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.ClearAllAsync());
        }
    }
}
