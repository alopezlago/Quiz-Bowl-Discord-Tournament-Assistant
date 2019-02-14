using Discord;
using Discord.Commands;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    [RequireOwner]
    [RequireUserPermission(GuildPermission.Administrator)]
    [RequireTournamentDirector]
    public class TournamentDirectorBotCommands : BotCommandsBase
    {
        public TournamentDirectorBotCommands(GlobalTournamentsManager globalManager)
            : base(globalManager)
        {
        }

        [Command("setup")]
        [Summary("Begins the setup phase of the tournament, where readers and teams can be added.")]
        public Task Setup([Remainder] [Summary("Name of the tournament.")] string tournamentName)
        {
            return this.HandleCommand(commandHandler => commandHandler.Setup(tournamentName));
        }

        // TODO: Make this add the reaction to the join message, if possible.
        [Command("addPlayer")]
        [Summary("Adds a player to a team.")]
        public Task AddPlayer(
            [Summary("Member to add as the player (as a @mention).")] IGuildUser user,
            [Remainder] [Summary("Team name.")] string teamName)
        {
            return this.HandleCommand(commandHandler => commandHandler.AddPlayer(user, teamName));
        }

        // TODO: Make this remove the reaction from the join message, if possible.
        [Command("removePlayer")]
        [Summary("Removes a player from a team.")]
        public Task RemovePlayer(
            [Summary("Member to add as the player (as a @mention).")] IGuildUser user)
        {
            return this.HandleCommand(commandHandler => commandHandler.RemovePlayer(user));
        }

        [Command("getPlayers")]
        [Summary("Gets the players in the current tournament, grouped by their team.")]
        public Task GetPlayers()
        {
            return this.HandleCommand(commandHandler => commandHandler.GetPlayers());
        }

        [Command("start")]
        [Summary("Starts the current tournament")]
        public Task Start()
        {
            return this.HandleCommand(commandHandler => commandHandler.Start());
        }

        [Command("back")]
        [Summary("Undoes the current stage and returns to the previous stage.")]
        public Task Back()
        {
            return this.HandleCommand(commandHandler => commandHandler.Back());
        }

        [Command("switchreaders")]
        [Summary("Switches the two readers.")]
        public Task SwitchReader(
            [Summary("Old reader to replace (as a @mention).")] IGuildUser oldReaderUser,
            [Summary("New reader (as a @mention).")] IGuildUser newReaderUser)
        {
            return this.HandleCommand(commandHandler => commandHandler.SwitchReader(oldReaderUser, newReaderUser));
        }

        [Command("finals")]
        [Summary("Sets up a room for the finals participants and reader.")]
        public Task Finals(
            [Summary("Reader for the finals (as a @mention).")] IGuildUser readerUser,
            [Remainder] [Summary("Name of the two teams in the finals, separated by a comma.")] string rawTeamNameParts)
        {
            return this.HandleCommand(commandHandler => commandHandler.Finals(readerUser, rawTeamNameParts));
        }

        [Command("end")]
        [Summary("Ends the current tournament.")]
        public Task End()
        {
            return this.HandleCommand(commandHandler => commandHandler.End());
        }
    }
}
