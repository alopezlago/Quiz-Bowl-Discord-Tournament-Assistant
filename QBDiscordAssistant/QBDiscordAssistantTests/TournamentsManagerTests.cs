using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using QBDiscordAssistant.Tournament;

namespace QBDiscordAssistantTests
{
    [TestClass]
    public class TournamentsManagerTests
    {
        private const string DefaultTournamentName = "Tournament1";

        [TestMethod]
        public void AddOrUpdateTournamentAddsThenUpdates()
        {
            const string originalTournamentName = "Tournament1";
            const string updatedTournamentName = "Tournament2";
            TournamentsManager manager = new TournamentsManager()
            {
                GuildId = 1
            };

            TournamentState originalState = new TournamentState(1, originalTournamentName);
            string[] expectedNames = new string[] { originalTournamentName, updatedTournamentName };

            for (int i = 0; i < expectedNames.Length; i++)
            {
                ITournamentState state = manager.AddOrUpdateTournament(
                    originalTournamentName,
                    originalState,
                    (name, oldState) => new TournamentState(1, updatedTournamentName));
                Assert.AreEqual(
                    expectedNames[i], state.Name, $"Unexpected tournament returned after {i + 1} call(s).");
            }
        }

        [TestMethod]
        public void TryGetTournament()
        {
            TournamentsManager manager = new TournamentsManager()
            {
                GuildId = 1
            };

            Assert.IsFalse(
                manager.TryGetTournament(DefaultTournamentName, out ITournamentState state), "No tournament state should exist.");

            TournamentState newState = new TournamentState(1, DefaultTournamentName);
            manager.AddOrUpdateTournament(DefaultTournamentName, newState, (name, oldState) => oldState);
            Assert.IsTrue(manager.TryGetTournament(DefaultTournamentName, out state), "Tournament state should exist.");
            Assert.AreEqual(newState, state, "Wrong tournament retrieved.");
        }

        [TestMethod]
        public void TrySetCurrentTournamentWithNoCurrentTournament()
        {
            TournamentsManager manager = new TournamentsManager()
            {
                GuildId = 1
            };

            TournamentState state = new TournamentState(1, DefaultTournamentName);
            TournamentState otherState = new TournamentState(1, DefaultTournamentName + "2");
            manager.AddOrUpdateTournament(DefaultTournamentName, state, (name, oldState) => oldState);

            Assert.IsTrue(
                manager.TrySetCurrentTournament(DefaultTournamentName, out string errorMessage),
                "Couldn't set current tournament.");
            manager.TryReadActionOnCurrentTournament(currentState =>
            {
                Assert.AreEqual(DefaultTournamentName, currentState.Name, "Unexpected tournament in TryReadAction");
                return Task.CompletedTask;
            });
            manager.TryReadWriteActionOnCurrentTournament(currentState =>
            {
                Assert.AreEqual(
                    DefaultTournamentName, currentState.Name, "Unexpected tournament in TryReadWriteAction (Action)");
            });
            manager.TryReadWriteActionOnCurrentTournament(currentState =>
            {
                Assert.AreEqual(
                    DefaultTournamentName, currentState.Name, "Unexpected tournament in TryReadWriteAction (Func)");
                return Task.CompletedTask;
            });
        }

        [TestMethod]
        public void TryClearCurrentTournament()
        {
            const string otherTournamentName = DefaultTournamentName + "2";
            TournamentsManager manager = new TournamentsManager()
            {
                GuildId = 1
            };

            TournamentState state = new TournamentState(1, DefaultTournamentName);
            TournamentState otherState = new TournamentState(1, otherTournamentName);
            manager.AddOrUpdateTournament(DefaultTournamentName, state, (name, oldState) => oldState);
            manager.AddOrUpdateTournament(otherTournamentName, otherState, (name, oldState) => oldState);

            Assert.IsTrue(
                manager.TrySetCurrentTournament(DefaultTournamentName, out string errorMessage),
                "Couldn't set current tournament initially.");
            Assert.IsTrue(manager.TryClearCurrentTournament(), "Couldn't clear current tournament.");

            // TrySetCurrentTournament should work again if we've just cleared it.
            Assert.IsTrue(
                manager.TrySetCurrentTournament(otherTournamentName, out errorMessage),
                "Couldn't set current tournament after clearing it.");
        }

        [TestMethod]
        public void TrySetCurrentTournamentWithUnaddedTournament()
        {
            TournamentsManager manager = new TournamentsManager()
            {
                GuildId = 1
            };

            Assert.IsFalse(
                manager.TrySetCurrentTournament(DefaultTournamentName, out string errorMessage),
                "Shouldn't be able to set current tournament with a non-existent tournament.");
            // TODO: If we go to using resources or string consts, check that value instead of just checking that the
            // message isn't null.
            Assert.IsNotNull(errorMessage, "Error message should not be null.");
        }

        [TestMethod]
        public void TrySetCurrentTournamentWhenCurrentTournamentAlreadyExists()
        {
            const string otherTournamentName = DefaultTournamentName + "2";
            TournamentsManager manager = new TournamentsManager()
            {
                GuildId = 1
            };

            TournamentState state = new TournamentState(1, DefaultTournamentName);
            TournamentState otherState = new TournamentState(1, otherTournamentName);
            manager.AddOrUpdateTournament(DefaultTournamentName, state, (name, oldState) => oldState);
            manager.AddOrUpdateTournament(otherTournamentName, otherState, (name, oldState) => oldState);

            Assert.IsTrue(
                manager.TrySetCurrentTournament(DefaultTournamentName, out string errorMessage),
                "First TrySet should succeed.");
            Assert.IsFalse(
                manager.TrySetCurrentTournament(otherTournamentName, out errorMessage),
                "Shouldn't be able to set the current tournament when one is already set.");
            // TODO: If we go to using resources or string consts, check that value instead of just checking that the
            // message isn't null.
            Assert.IsNotNull(errorMessage, "Error message should not be null.");
        }

        // TODO: Add tests to verify that the mutexes are blocking correctly.
    }
}
