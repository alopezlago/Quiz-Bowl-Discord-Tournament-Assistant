using System;
using System.Collections.Generic;

namespace QBDiscordAssistant.Tournament
{
    public class RoundRobinScheduleFactory : IScheduleFactory
    {
        private readonly int roundRobins;

        public RoundRobinScheduleFactory(int roundRobins)
        {
            if (roundRobins <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundRobins), $"roundRobins must be positive. Value: {roundRobins}");
            }

            this.roundRobins = roundRobins;
        }

        public Schedule Generate(ISet<Team> teams, ISet<Reader> readers)
        {
            if (teams.Count <= 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(teams), $"Must have more than 1 team. Count: {teams.Count}");
            }
            else if (readers.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(readers), $"Must have a reader.");
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
                        Reader = reader,
                        // TODO: Figure out how to generate the room, since we don't have the Id yet.
                        Room = new Room()
                        {
                            // The Id must be filled in later, or we need to pass in something which creates these rooms.
                            Reader = reader,
                            Name = $"Round{roundNumber}-{reader.Name}"
                        }
                    });
                }
            }

            RotateCells(topRow, bottomRow);
            return round;
        }

        private static void RotateCells(Team[] topRow, Team[] bottomRow)
        {
            // Circle method rotation: have two rows and fix the first number in the table. For each rotation:
            // - Push numbers in the top row to the right, and push numbers in the bottom row to the left.
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

            topRow[topRow.Length - 1] = leavingBottomRowTeam;
            for (int j = topRow.Length - 2; j > 1; j--)
            {
                topRow[j] = topRow[j - 1];
            }

            for (int j = 0; j < bottomRow.Length - 1; j++)
            {
                bottomRow[j] = bottomRow[j + 1];
            }

            bottomRow[bottomRow.Length - 1] = leavingTopRowTeam;
        }
    }
}
