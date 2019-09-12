using System;
using System.Collections.Generic;
using System.Linq;

namespace QBDiscordAssistant.Tournament
{
    public class RoundRobinScheduleFactory : IScheduleFactory
    {
        internal const int MaximumBrackets = 6;

        private readonly int roundRobins;

        public RoundRobinScheduleFactory(int roundRobins)
        {
            this.roundRobins = roundRobins;
        }

        public Schedule Generate(IEnumerable<ISet<Team>> teams, ISet<Reader> readers)
        {
            if (roundRobins <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundRobins), TournamentStrings.RoundRobinsMustBePositive(roundRobins));
            }

            int bracketsCount = teams.Count();
            if (bracketsCount > MaximumBrackets)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(teams), TournamentStrings.OnlyAtMostNBrackets(bracketsCount));
            }

            int teamsCount = teams.Sum(t => t.Count);
            if (teamsCount <= 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(teams), TournamentStrings.MustHaveMoreThanOneTeam(teamsCount));
            }
            else if (teams.Any(t => t.Count <= 1))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(teams), TournamentStrings.MustHaveMoreThanOneTeamPerBracket);
            }
            else if (readers.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(readers), TournamentStrings.MustHaveReader);
            }

            Schedule schedule = new Schedule();
            List<Bracket> brackets = this.CreateBrackets(teams, readers);
            int maximumRounds = brackets.Max(bracket => bracket.Rounds);
            Round[] rounds = Enumerable.Range(0, maximumRounds)
                .Select(number => new Round())
                .ToArray();

            // TODO: Investigate if it makes sense to parallelize this
            foreach (Bracket bracket in brackets)
            {
                for (int i = 0; i < bracket.Rounds; i++)
                {
                    this.AddBracketGamesToRound(rounds[i], bracket, i);
                }
            }

            foreach (Round round in rounds)
            {
                schedule.AddRound(round);
            }

            return schedule;
        }

        public Schedule Generate(ISet<Team> teams, ISet<Reader> readers)
        {
            ISet<Team>[] teamsByBracket = teams
                .GroupBy(team => team.Bracket)
                .Select(grouping => new HashSet<Team>(grouping))
                .ToArray();
            return this.Generate(teamsByBracket, readers);
        }

        private static void RotateCells(Team[] topRow, Team[] bottomRow)
        {
            // Circle method rotation: have two rows and fix the first number in the table. For each rotation:
            // - Push numbers in the top row to the right, and push numbers in the bottom row to the left (clockwise)
            //    - If a number leaves the top row, put it at the end of the bottom row.
            //    - If a number leaves the bottom row, put it at the start of the top row.
            //    - If a bye is needed (odd number of teams), use a dummy team (null) to indicate which team gets the
            //      bye. We can rotate this dummy team as normal.

            // We don't need to rotate the cells if there are only two teams, since they must play each other.
            if (topRow.Length == 1)
            {
                return;
            }

            // Save these since we will overwrite them.
            Team leavingTopRowTeam = topRow[topRow.Length - 1];
            Team leavingBottomRowTeam = bottomRow[0];

            // Push teams. We want to skip the first cell in the top row, so do > 1, since 1 should belong to the
            // team leaving the bottom row.
            for (int j = topRow.Length - 1; j > 1; j--)
            {
                topRow[j] = topRow[j - 1];
            }

            // 0 is the fixed element, so go to the next one. We return early if there's only one element in the top
            // row, so there's no danger of an index-out-of-bounds exception
            topRow[1] = leavingBottomRowTeam;

            for (int j = 0; j < bottomRow.Length - 1; j++)
            {
                bottomRow[j] = bottomRow[j + 1];
            }

            bottomRow[bottomRow.Length - 1] = leavingTopRowTeam;
        }

        private void AddBracketGamesToRound(Round round, Bracket bracket, int roundNumber)
        {
            IEnumerable<Reader> readersEnumerable = bracket.Readers;
            if (roundNumber % 2 == 1)
            {
                // Reverse the readers every other round. This should ensure that, when there are over 4 teams, that no
                // team has the same reader each round
                // This approach does have the downside of doing O(|readers|) work every other round. If this is too
                // slow, we should save the reversed enumerable, then pass both in.
                readersEnumerable = readersEnumerable.Reverse();
            }

            using (IEnumerator<Reader> readers = readersEnumerable.GetEnumerator())
            {
                GenerateGameForRound(round, readers, bracket.TopRow, bracket.BottomRow, bracket.HasBye);
            }
        }

        private List<Bracket> CreateBrackets(IEnumerable<ISet<Team>> teams, ISet<Reader> readers)
        {
            int teamsCount = teams.Count();
            if (teamsCount / 2 > readers.Count)
            {
                throw new ArgumentException(TournamentStrings.NotEnoughReadersForTeams(readers.Count, teamsCount));
            }

            List<Bracket> brackets = new List<Bracket>();
            using (IEnumerator<Reader> readersEnumerator = readers.GetEnumerator())
            {
                foreach (ISet<Team> teamsInBracket in teams)
                {
                    // We're adding a reader for every 2 teams, so start at 1 and increment by 2.
                    List<Reader> bracketReaders = new List<Reader>();
                    for (int i = 1; i < teamsInBracket.Count; i += 2)
                    {
                        readersEnumerator.MoveNext();
                        bracketReaders.Add(readersEnumerator.Current);
                    }

                    bool hasBye = teamsInBracket.Count % 2 == 1;
                    int rounds = checked(this.roundRobins * (hasBye ? teamsInBracket.Count : teamsInBracket.Count - 1));

                    this.GetInitialRows(teamsInBracket, out Team[] topRow, out Team[] bottomRow);

                    brackets.Add(new Bracket()
                    {
                        Readers = bracketReaders,
                        Teams = teamsInBracket,
                        TopRow = topRow,
                        BottomRow = bottomRow,
                        Rounds = rounds,
                        HasBye = hasBye
                    });
                }
            }

            return brackets;
        }

        private void GenerateGameForRound(
            Round round, IEnumerator<Reader> readers, Team[] topRow, Team[] bottomRow, bool hasBye)
        {
            int gamesCount = hasBye ? topRow.Length - 1 : topRow.Length;
            for (int i = 0; i < topRow.Length; i++)
            {
                Team firstTeam = topRow[i];
                if (firstTeam == null)
                {
                    // Skip the bye team
                    continue;
                }

                Team secondTeam = bottomRow[i];
                if (secondTeam == null)
                {
                    // Skip the bye team
                    continue;
                }

                readers.MoveNext();
                Reader reader = readers.Current;
                round.Games.Add(new Game()
                {
                    Teams = new Team[] { firstTeam, secondTeam },
                    Reader = reader
                });
            }

            RotateCells(topRow, bottomRow);
        }

        private void GetInitialRows(ISet<Team> teams, out Team[] topRow, out Team[] bottomRow)
        {
            // Generates a schedule using the Circle method. Initialize the two rows with the teams based on the order
            // they were created.
            int rowLength = (teams.Count + 1) / 2;
            topRow = new Team[rowLength];
            bottomRow = new Team[rowLength];
            using (IEnumerator<Team> enumerator = teams.GetEnumerator())
            {
                for (int i = 0; i < topRow.Length; i++)
                {
                    enumerator.MoveNext();
                    topRow[i] = enumerator.Current;
                }

                for (int i = 0; i < bottomRow.Length; i++)
                {
                    enumerator.MoveNext();
                    bottomRow[i] = enumerator.Current;
                }
            }
        }

        private class Bracket
        {
            public IEnumerable<Reader> Readers { get; set; }

            public ISet<Team> Teams { get; set; }

            public Team[] TopRow { get; set; }

            public Team[] BottomRow { get; set; }

            public int Rounds { get; set; }

            public bool HasBye { get; set; }
        }
    }
}

// Note: one possible optimization in Schedule would be to generate the rounds once, then add the first set of rounds
// this.roundRobins number of times. The downside is that the Round objects (and Game objects in them) are all the same,
// so if we decide to include unique information in them (maybe round number or events), we'll have to fall back to the
// original approach. Here is the code to reuse the rounds:
////Schedule schedule = new Schedule();
////bool hasBye = teams.Count % 2 == 1;
////int roundsCount = hasBye ? teams.Count : teams.Count - 1;
////Round[] rounds2 = new Round[roundsCount];
////for (int i = 0; i<rounds2.Length; i++)
////{
////    Round round = GenerateRound(i, topRow, bottomRow, readers, hasBye);
////schedule.AddRound(round);
////    rounds2[i] = round;
////}

////for (int roundRobinsCount = 1; roundRobinsCount< this.roundRobins; roundRobinsCount++)
////{
////    for (int i = 0; i<rounds2.Length; i++)
////    {
////        schedule.AddRound(rounds2[i]);
////    }
////}