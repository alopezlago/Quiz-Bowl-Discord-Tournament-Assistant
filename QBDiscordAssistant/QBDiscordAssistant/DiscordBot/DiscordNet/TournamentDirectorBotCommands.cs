using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    [RequireTournamentDirector]
    [SuppressMessage(
        "Design",
        "CA1062:Validate arguments of public methods",
        Justification = "Dependency injection will fail before null value passed in")]
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
            this.Logger.Information(
                "{id} is attempting to set up {tournamentName}", this.Context.User.Id, tournamentName);
            return this.HandleCommandAsync(commandHandler => commandHandler.SetupTournamentAsync(tournamentName));
        }

        // TODO: Make this add the reaction to the join message, if possible.
        [Command("addPlayer")]
        [Summary("Adds a player to a team.")]
        public Task AddPlayer(
            [Summary("Member to add as the player (as a @mention).")] IGuildUser user,
            [Remainder] [Summary("Team name.")] string teamName)
        {
            this.Logger.Information(
                "{0} is adding the user {1} to the team {2}", this.Context.User.Id, user.Id, teamName);
            return this.HandleCommandAsync(commandHandler => commandHandler.AddPlayerAsync(user, teamName));
        }

        // TODO: Make this remove the reaction from the join message, if possible.
        [Command("removePlayer")]
        [Summary("Removes a player from a team.")]
        public Task RemovePlayer(
            [Summary("Member to add as the player (as a @mention).")] IGuildUser user)
        {
            this.Logger.Information(
                "{0} is removing the user {1} from their team", this.Context.User.Id, user.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.RemovePlayerAsync(user));
        }

        [Command("getPlayers")]
        [Summary("Gets the players in the current tournament, grouped by their team.")]
        public Task GetPlayers()
        {
            this.Logger.Information("{id} is requesting all of the current players", this.Context.User.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.GetPlayersAsync());
        }

        [Command("rebracket")]
        [Summary("Create new rooms for the new brackets. The teams specified must already exist")]
        public Task Rebracket()
        {
            this.Logger.Information("{id} is rebracketing", this.Context.User.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.RebracketAsync());
        }

        [Command("start")]
        [Summary("Starts the current tournament")]
        public Task Start()
        {
            this.Logger.Information("{id} is starting the current tournament", this.Context.User.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.StartAsync());
        }

        [Command("back")]
        [Summary("Undoes the current stage and returns to the previous stage.")]
        public Task Back()
        {
            this.Logger.Information("{id} is undoing their previous setup action", this.Context.User.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.GoBackAsync());
        }

        [Command("switchreaders")]
        [Summary("Switches the two readers.")]
        public Task SwitchReader(
            [Summary("Old reader to replace (as a @mention).")] IGuildUser oldReaderUser,
            [Summary("New reader (as a @mention).")] IGuildUser newReaderUser)
        {
            this.Logger.Information(
                "{0} is replacing reader {1} with reader {2}", this.Context.User.Id, oldReaderUser.Id, newReaderUser.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.SwitchReaderAsync(oldReaderUser, newReaderUser));
        }

        [Command("finals")]
        [Summary("Sets up a room for the finals participants and reader.")]
        public Task Finals(
            [Summary("Reader for the finals (as a @mention).")] IGuildUser readerUser,
            [Remainder] [Summary("Name of the two teams in the finals, separated by a comma.")] string rawTeamNameParts)
        {
            this.Logger.Information(
                "{0} is attempting to start finals with the reader {1} and teams {2}",
                this.Context.User.Id,
                readerUser.Id,
                rawTeamNameParts);
            return this.HandleCommandAsync(commandHandler => commandHandler.SetupFinalsAsync(readerUser, rawTeamNameParts));
        }

        [Command("end")]
        [Summary("Ends the current tournament.")]
        public Task End()
        {
            this.Logger.Information("{0} is ending the tournament", this.Context.User.Id);
            return this.HandleCommandAsync(commandHandler => commandHandler.EndTournamentAsync());
        }
    }
}
