using System;
using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public class RoundRobinScheduleFactory : IScheduleFactory
    {
        private readonly int roundRobins;

        public RoundRobinScheduleFactory(int roundRobins)
        {
            this.roundRobins = roundRobins;
        }

        public Schedule Generate(ISet<Team> teams, ISet<Reader> readers)
        {
            if (roundRobins <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundRobins), TournamentStrings.RoundRobinsMustBePositive(roundRobins));
            }
            else if (teams.Count <= 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(teams), TournamentStrings.MustHaveMoreThanOneTeam(teams.Count));
            }
            else if (readers.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(readers), TournamentStrings.MustHaveReader);
            }

            Schedule schedule = new Schedule();
            bool hasBye = teams.Count % 2 == 1;
            int rounds = checked(this.roundRobins * (hasBye ? teams.Count : teams.Count - 1));

            // Generates a schedule using the Circle method. Initialize the two rows with the teams based on the order
            // they were created.
            int rowLength = (teams.Count + 1) / 2;
            Team[] topRow = new Team[rowLength];
            Team[] bottomRow = new Team[rowLength];
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

            for (int i = 0; i < rounds; i++)
            {
                Round round = GenerateRound(i, topRow, bottomRow, readers, hasBye);
                schedule.AddRound(round);
            }

            return schedule;
        }

        private Round GenerateRound(int roundNumber, Team[] topRow, Team[] bottomRow, ISet<Reader> readers, bool hasBye)
        {
            int gamesCount = hasBye ? topRow.Length - 1 : topRow.Length;
            Round round = new Round();
            using (IEnumerator<Reader> readersEnumerator = readers.GetEnumerator())
            {
                // If there's no bye team, then topRow.Length is the same as gamesCount. Otherwise, we'll skip at least
                // one potential game if we go through the whole top row, so the count will still match.
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

                    readersEnumerator.MoveNext();
                    Reader reader = readersEnumerator.Current;
                    round.Games.Add(new Game()
                    {
                        Teams = new Team[] { firstTeam, secondTeam },
                        Reader = reader
                    });
                }
            }

            RotateCells(topRow, bottomRow);
            return round;
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