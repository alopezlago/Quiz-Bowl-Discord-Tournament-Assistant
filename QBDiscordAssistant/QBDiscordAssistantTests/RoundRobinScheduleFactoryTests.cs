using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class RoundRobinScheduleFactoryTests
    {
        [TestMethod]
        public void TwoTeamSchedule()
        {
            RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(1);
            CreateTeamsAndReaders(2, out ISet<Team> teams, out ISet<Reader> readers);
            Schedule schedule = scheduleFactory.Generate(teams, readers);

            Assert.AreEqual(1, schedule.Rounds.Count, "Unexpected number of rounds.");
            Round round = schedule.Rounds[0];
            Assert.AreEqual(1, round.Games.Count, "Unexpected number of games.");

            Game game = round.Games[0];
            Assert.AreEqual(readers.First(), game.Reader, "Unexpected reader.");

            string teamNames = string.Join(",", game.Teams.Select(team => team.Name));
            Assert.AreEqual(
                2,
                game.Teams.Intersect(teams).Count(),
                $"Both teams were not in the game. Teams in the game: {teamNames}");
        }

        [TestMethod]
        public void MultipleTeamSchedules()
        {
            // At minimum we need to do 3-6, since that's when we test the boundary conditions.
            for (int i = 3; i < 10; i++)
            {
                VerifySchedule(i);
            }
        }

        [TestMethod]
        public void MultipleTeamSchedulesInTwoBrackets()
        {
            // At minimum we need to do 3-6, since that's when we test the boundary conditions.
            for (int i = 4; i < 10; i++)
            {
                int[] teamsPerBracket = new int[] { i / 2, i / 2 + (i % 2) };
                VerifyMultiBracketSchedule(teamsPerBracket);
            }
        }

        [TestMethod]
        public void NegativeRoundRobinsFails()
        {
            RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(-1);
            CreateTeamsAndReaders(2, out ISet<Team> teams, out ISet<Reader> readers);
            Assert.ThrowsException<InvalidOperationException>(() => scheduleFactory.Generate(teams, new HashSet<Reader>()));
        }

        [TestMethod]
        public void NoReadersFails()
        {
            RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(1);
            CreateTeamsAndReaders(2, out ISet<Team> teams, out ISet<Reader> readers);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => scheduleFactory.Generate(teams, new HashSet<Reader>()));
        }

        [TestMethod]
        public void NoRoundRobinsFails()
        {
            RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(0);
            CreateTeamsAndReaders(2, out ISet<Team> teams, out ISet<Reader> readers);
            Assert.ThrowsException<InvalidOperationException>(() => scheduleFactory.Generate(teams, new HashSet<Reader>()));
        }

        [TestMethod]
        public void NoTeamsFails()
        {
            RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(1);
            CreateTeamsAndReaders(2, out ISet<Team> teams, out ISet<Reader> readers);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => scheduleFactory.Generate(new HashSet<Team>(), readers));
        }

        [TestMethod]
        public void ZeroTeamsFails()
        {
            RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(1);
            CreateTeamsAndReaders(2, out ISet<Team> teams, out ISet<Reader> readers);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => scheduleFactory.Generate(new HashSet<Team>(), readers));
        }

        [TestMethod]
        public void OneTeamFails()
        {
            RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(1);
            CreateTeamsAndReaders(1, out ISet<Team> teams, out ISet<Reader> readers);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => scheduleFactory.Generate(teams, readers));
        }

        private static int GetRoundCountInOneRoundRobin(int teamsCount)
        {
            return teamsCount % 2 == 0 ? teamsCount - 1 : teamsCount;
        }

        private static void VerifySchedule(int teamsCount)
        {
            CreateTeamsAndReaders(teamsCount, out ISet<Team> teams, out ISet<Reader> readers);

            for (int i = 1; i < 3; i++)
            {
                RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(i);
                Schedule schedule = scheduleFactory.Generate(teams, readers);
                int expectedNumberOfRounds = i * GetRoundCountInOneRoundRobin(teamsCount);
                VerifySchedule(schedule, teams, expectedNumberOfRounds);
            }
        }

        private static void VerifyMultiBracketSchedule(int[] teamsInBracketCount)
        {
            int totalTeams = teamsInBracketCount.Sum();
            CreateTeamsAndReaders(totalTeams, out ISet<Team> teams, out ISet<Reader> readers);

            List<ISet<Team>> teamsInBrackets = new List<ISet<Team>>();
            using (IEnumerator<Team> teamsEnumerator = teams.GetEnumerator())
            {
                foreach (int bracketSize in teamsInBracketCount)
                {
                    ISet<Team> teamsInBracket = new HashSet<Team>();
                    for (int i = 0; i < bracketSize; i++)
                    {
                        teamsEnumerator.MoveNext();
                        teamsInBracket.Add(teamsEnumerator.Current);
                    }

                    teamsInBrackets.Add(teamsInBracket);
                }
            }

            for (int i = 1; i < 3; i++)
            {
                RoundRobinScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(i);
                Schedule schedule = scheduleFactory.Generate(teamsInBrackets, readers);
                int expectedNumberOfRounds = i * GetRoundCountInOneRoundRobin(teamsInBrackets.Max(t => t.Count));
                VerifyMultiBracketSchedule(schedule, teamsInBrackets, i, expectedNumberOfRounds);
            }
        }

        private static void VerifySchedule(
            Schedule schedule, ISet<Team> teams, int roundsCount)
        {
            Assert.AreEqual(
                roundsCount, schedule.Rounds.Count, $"Unexpected number of rounds generated. ({teams.Count} teams)");

            Dictionary<Team, List<Team>> teamOpponentsMap = new Dictionary<Team, List<Team>>();
            foreach (Game game in schedule.Rounds.SelectMany(round => round.Games))
            {
                foreach (Team team in game.Teams)
                {
                    if (!teamOpponentsMap.TryGetValue(team, out List<Team> teamOpponents))
                    {
                        teamOpponents = new List<Team>();
                        teamOpponentsMap[team] = teamOpponents;
                    }

                    teamOpponents.AddRange(game.Teams.Where(t => t != team));
                }
            }

            int otherTeamsCount = teams.Count - 1;
            int roundCountInOneRoundRobin = GetRoundCountInOneRoundRobin(teams.Count);
            int matchesPerTeam = otherTeamsCount * (roundsCount / roundCountInOneRoundRobin);
            Assert.AreEqual(
                teams.Count,
                teamOpponentsMap.Count,
                $"Unexpected number of teams (for {roundsCount} rounds and {teams.Count} teams).");
            Team shortchangedTeam = teamOpponentsMap.FirstOrDefault(pair => pair.Value.Count != matchesPerTeam).Key;
            Assert.IsNull(
               shortchangedTeam,
               $"Team with name {shortchangedTeam?.Name} did not play exactly {matchesPerTeam} games (for {roundsCount} rounds and {teams.Count} teams).");
            Team skippingTeam = teamOpponentsMap
                .FirstOrDefault(pair => pair.Value.Distinct().Count() != otherTeamsCount).Key;
            Assert.IsNull(
                skippingTeam,
                $"Team with name {skippingTeam?.Name} did not all opponent teams (for {roundsCount} rounds).");
        }

        private static void VerifyMultiBracketSchedule(
            Schedule schedule,  List<ISet<Team>> teamsInBrackets, int roundRobinsCount, int roundsCount)
        {
            Assert.AreEqual(
                roundsCount,
                schedule.Rounds.Count,
                $"Unexpected number of rounds generated. ({teamsInBrackets.Sum(teams => teams.Count)} teams.");

            Dictionary<Team, List<Team>> teamOpponentsMap = new Dictionary<Team, List<Team>>();
            foreach (Game game in schedule.Rounds.SelectMany(round => round.Games))
            {
                foreach (Team team in game.Teams)
                {
                    if (!teamOpponentsMap.TryGetValue(team, out List<Team> teamOpponents))
                    {
                        teamOpponents = new List<Team>();
                        teamOpponentsMap[team] = teamOpponents;
                    }

                    teamOpponents.AddRange(game.Teams.Where(t => t != team));
                }
            }

            foreach (ISet<Team> teams in teamsInBrackets)
            {
                int otherTeamsCount = teams.Count - 1;
                int roundCountInOneRoundRobin = GetRoundCountInOneRoundRobin(teams.Count);
                int matchesPerTeam = otherTeamsCount * roundRobinsCount;

                // We need a map specific to the bracket, since teams in one bracket shouldn't play teams in another
                // bracket.
                Dictionary<Team, List<Team>> bracketOpponentsMap = teamOpponentsMap
                    .Where(kvp => teams.Contains(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                Assert.AreEqual(
                    teams.Count,
                    bracketOpponentsMap.Count,
                    $"Unexpected number of teams (for {roundsCount} rounds and {teams.Count} teams).");
                Team shortchangedTeam = bracketOpponentsMap.FirstOrDefault(pair => pair.Value.Count != matchesPerTeam).Key;
                Assert.IsNull(
                   shortchangedTeam,
                   $"Team with name {shortchangedTeam?.Name} did not play exactly {matchesPerTeam} games (for {roundsCount} rounds and {teams.Count} teams).");
                Team skippingTeam = bracketOpponentsMap
                    .FirstOrDefault(pair => pair.Value.Distinct().Count() != otherTeamsCount).Key;
                Assert.IsNull(
                    skippingTeam,
                    $"Team with name {skippingTeam?.Name} did not all opponent teams (for {roundsCount} rounds).");
            }
        }

        private static void CreateTeamsAndReaders(int teamsCount, out ISet<Team> teams, out ISet<Reader> readers)
        {
            teams = new HashSet<Team>(teamsCount);
            readers = new HashSet<Reader>();

            for (int i = 0; i < teamsCount; i++)
            {
                teams.Add(new Team()
                {
                    Name = $"Team{i}"
                });

                if (i % 2 == 1)
                {
                    readers.Add(new Reader()
                    {
                        Id = (ulong)(i / 2),
                        Name = $"Reader{i / 2}",
                    });
                }
            }
        }
    }
}
