using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QBDiscordAssistant;
using QBDiscordAssistant.DiscordBot.DiscordNet;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class GeneralBotCommandHandlerTests : CommandHandlerTestBase
    {
        [TestMethod]
        public async Task ScheduleRequiresCurrentTournament()
        {
            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            await commandHandler.GetScheduleAsync();
            messageStore.VerifyDirectMessages(
                BotStrings.UnableToPerformCommand(TournamentStrings.NoCurrentTournamentRunning));
        }

        [TestMethod]
        public async Task SimplestSchedule()
        {
            const string readerName = "#Reader";
            const string firstTeamName = "#TeamA";
            const string secondTeamName = "#TeamB";

            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore, guildId: DefaultGuildId);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());

            HashSet<Team> teams = new HashSet<Team>()
            {
                new Team()
                {
                    Name = firstTeamName
                },
                new Team()
                {
                    Name = secondTeamName
                }
            };
            HashSet<Reader> readers = new HashSet<Reader>()
            {
                new Reader()
                {
                    Id = 0,
                    Name = readerName
                }
            };
            RoundRobinScheduleFactory factory = new RoundRobinScheduleFactory(2, 0);
            Schedule schedule = factory.Generate(teams, readers);

            ITournamentState state = new TournamentState(DefaultGuildId, "T");
            state.Schedule = schedule;
            manager.AddOrUpdateTournament(state.Name, state, (name, oldState) => oldState);
            Assert.IsTrue(
                manager.TrySetCurrentTournament(state.Name, out string errorMessage),
                $"Failed to set the tournament: '{errorMessage}'");
            globalManager.GetOrAdd(DefaultGuildId, id => manager);

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            await commandHandler.GetScheduleAsync();
            Assert.AreEqual(1, messageStore.ChannelEmbeds.Count, "Unexpected number of embeds");

            string embed = messageStore.ChannelEmbeds[0];
            for (int round = 0; round < schedule.Rounds.Count; round++)
            {
                Assert.IsTrue(
                    embed.Contains(BotStrings.RoundNumber(round + 1)), 
                    $"Round {round + 1} not found in embed. Embed: '{embed}'");
                string expectedGame = BotStrings.ScheduleLine(
                    readerName, schedule.Rounds[round].Games[0].Teams.Select(team => team.Name).ToArray());
                Assert.IsTrue(
                    embed.Contains(expectedGame),
                    $"Game '{expectedGame}' not foudn in embed. Embed: '{embed}'");
            }
        }

        [TestMethod]
        public async Task ScheduleRequiresMultipleEmbeds()
        {
            const int teamsCount = 50;
            const int readersCount = teamsCount / 2;

            MessageStore messageStore = new MessageStore();
            ICommandContext context = this.CreateCommandContext(messageStore, guildId: DefaultGuildId);
            GlobalTournamentsManager globalManager = new GlobalTournamentsManager();
            TournamentsManager manager = globalManager.GetOrAdd(DefaultGuildId, id => new TournamentsManager());

            HashSet<Team> teams = new HashSet<Team>(
                Enumerable.Range(1, teamsCount).Select(id => new Team() { Name = $"#Team{id}" }));
            HashSet<Reader> readers = new HashSet<Reader>(
                Enumerable.Range(1, readersCount).Select(id => new Reader() { Id = (ulong)id, Name = $"#Reader{id}" }));

            RoundRobinScheduleFactory factory = new RoundRobinScheduleFactory(1, 0);
            Schedule schedule = factory.Generate(teams, readers);

            ITournamentState state = new TournamentState(DefaultGuildId, "T");
            state.Schedule = schedule;
            manager.AddOrUpdateTournament(state.Name, state, (name, oldState) => oldState);
            Assert.IsTrue(
                manager.TrySetCurrentTournament(state.Name, out string errorMessage),
                $"Failed to set the tournament: '{errorMessage}'");
            globalManager.GetOrAdd(DefaultGuildId, id => manager);

            BotCommandHandler commandHandler = new BotCommandHandler(context, globalManager);

            await commandHandler.GetScheduleAsync();
            Assert.IsTrue(
                messageStore.ChannelEmbeds.Count > 1, 
                $"Expected more than 1 embed, got {messageStore.ChannelEmbeds.Count}");

            string allEmbeds = string.Join('\n', messageStore.ChannelEmbeds);
            for (int round = 0; round < schedule.Rounds.Count; round++)
            {
                Assert.IsTrue(
                    allEmbeds.Contains(BotStrings.RoundNumber(round + 1)),
                    $"Round {round + 1} not found in the embeds. Embeds: '{allEmbeds}'");
                foreach (Game game in schedule.Rounds[round].Games)
                {
                    string expectedGame = BotStrings.ScheduleLine(
                        game.Reader.Name, game.Teams.Select(team => team.Name).ToArray());
                    Assert.IsTrue(
                        allEmbeds.Contains(expectedGame),
                        $"Game '{expectedGame}' not foudn in embed. Embed: '{allEmbeds}'");
                }
            }
        }
    }
}
