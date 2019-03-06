using System;
using System.Collections.Concurrent;
using System.Threading;

namespace QBDiscordAssistant.Tournament
{
    // TODO: Find a way to persist this state on disk so that we can recover when the bot is closed/goes down.
    // If we want to do that we should take in an interface to persist this state, and add methods for adding/removing
    // tournaments.
    public class TournamentsManager
    {
        private const int mutexTimeoutMs = 60 * 1000; // 1 minute

        private readonly ConcurrentDictionary<string, ITournamentState> pendingTournaments;
        private ReaderWriterLockSlim currentTournamentLock;

        public TournamentsManager()
        {
            this.pendingTournaments = new ConcurrentDictionary<string, ITournamentState>(StringComparer.CurrentCultureIgnoreCase);
            this.currentTournamentLock = new ReaderWriterLockSlim();
        }

        public ulong GuildId { get; set; }

        private ITournamentState CurrentTournament { get; set; }

        public Result<T> TryReadActionOnCurrentTournament<T>(Func<IReadOnlyTournamentState, T> tournamentStateFunc)
        {
            if (!this.currentTournamentLock.TryEnterReadLock(mutexTimeoutMs))
            {
                return Result<T>.CreateFailureResult(TournamentStrings.UnableAccessCurrentTournament);
            }

            try
            {
                if (this.CurrentTournament == null)
                {
                    return Result<T>.CreateFailureResult(TournamentStrings.NoCurrentTournamentRunning);
                }

                T result = tournamentStateFunc(this.CurrentTournament);
                return Result<T>.CreateSuccessResult(result);
            }
            finally
            {
                this.currentTournamentLock.ExitReadLock();
            }
        }

        public bool TryReadActionOnCurrentTournament(Action<IReadOnlyTournamentState> tournamentStateAction)
        {
            Func<IReadOnlyTournamentState, bool> tournamentStateFunc = (state) =>
            {
                tournamentStateAction(state);
                return true;
            };
            return this.TryReadActionOnCurrentTournament<bool>(tournamentStateFunc).Success;
        }

        public Result<T> TryReadWriteActionOnCurrentTournament<T>(Func<ITournamentState, T> tournamentStateTask)
        {
            if (!this.currentTournamentLock.TryEnterWriteLock(mutexTimeoutMs))
            {
                return Result<T>.CreateFailureResult(TournamentStrings.UnableAccessCurrentTournament);
            }

            try
            {
                if (this.CurrentTournament == null)
                {
                    return Result<T>.CreateFailureResult(TournamentStrings.NoCurrentTournamentRunning);
                }

                T result = tournamentStateTask(this.CurrentTournament);
                return Result<T>.CreateSuccessResult(result);
            }
            finally
            {
                this.currentTournamentLock.ExitWriteLock();
            }
        }

        public bool TryReadWriteActionOnCurrentTournament(Action<ITournamentState> tournamentStateAction)
        {
            Func<ITournamentState, bool> tournamentStateFunc = (state) =>
            {
                tournamentStateAction(state);
                return true;
            };
            return this.TryReadWriteActionOnCurrentTournament<bool>(tournamentStateFunc).Success;
        }

        public ITournamentState AddOrUpdateTournament(
            string name, ITournamentState addState, Func<string, ITournamentState, ITournamentState> updateStateFunc)
        {
            return this.pendingTournaments.AddOrUpdate(name, addState, updateStateFunc);
        }

        public bool TryGetTournament(string name, out ITournamentState state)
        {
            return this.pendingTournaments.TryGetValue(name, out state);
        }

        public bool TrySetCurrentTournament(string name, out string errorMessage)
        {
            if (!(this.currentTournamentLock.TryEnterUpgradeableReadLock(mutexTimeoutMs) &&
                this.currentTournamentLock.TryEnterWriteLock(mutexTimeoutMs)))
            {
                errorMessage = TournamentStrings.UnableAccessCurrentTournament;
                return false;
            }

            try
            {
                if (this.CurrentTournament != null)
                {
                    errorMessage = string.Format(
                        TournamentStrings.TournamentAlreadyRunning, this.CurrentTournament.Name);
                    return false;
                }

                if (!this.pendingTournaments.TryGetValue(name, out ITournamentState state))
                {
                    errorMessage = string.Format(TournamentStrings.TournamentCannotBeFound, name);
                    return false;
                }

                this.CurrentTournament = state;

                if (!this.pendingTournaments.TryRemove(name, out state))
                {
                    // Couldn't set the current tournament, so roll back the change
                    errorMessage = string.Format(
                        TournamentStrings.CannotMoveTournamentFromPending, this.CurrentTournament.Name);
                    this.CurrentTournament = null;
                    return false;
                }

                errorMessage = null;
                return true;
            }
            finally
            {
                this.currentTournamentLock.ExitWriteLock();
                this.currentTournamentLock.ExitUpgradeableReadLock();
            }
        }

        public bool TryClearCurrentTournament()
        {
            if (!this.currentTournamentLock.TryEnterWriteLock(mutexTimeoutMs))
            {
                return false;
            }

            this.CurrentTournament = null;
            this.currentTournamentLock.ExitWriteLock();

            return true;
        }
    }
}
