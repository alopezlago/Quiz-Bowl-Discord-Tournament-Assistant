﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using QBDiscordAssistant.Tournament;
using Serilog;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public sealed class BotEventHandler : IDisposable
    {
        // TODO: Add wrapper class/interface for the client to let us test the event handlers.
        // The events themselves pass in SocketMessages, so perhaps grab some fields and abstract the logic. Let's do a
        // straight transfer first.
        public BotEventHandler(DiscordSocketClient client, GlobalTournamentsManager globalManager)
        {
            Verify.IsNotNull(client, nameof(client));

            this.IsDisposed = false;
            this.Client = client;
            this.GlobalManager = globalManager;

            this.Client.MessageReceived += this.OnMessageReceived;
            this.Client.ReactionAdded += this.OnReactionAdded;
            this.Client.ReactionRemoved += this.OnReactionRemoved;

            this.Logger = Log.ForContext<BotEventHandler>();
        }

        // TODO: Add wrapper class/interface for the client to let us test the event handlers.
        private BaseSocketClient Client { get; }

        private bool IsDisposed { get; set; }

        private GlobalTournamentsManager GlobalManager { get; }

        private ILogger Logger { get; }

        public void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.Logger.Verbose("Disposing");
                this.IsDisposed = true;
                this.Client.MessageReceived += this.OnMessageReceived;
                this.Client.ReactionAdded += this.OnReactionAdded;
                this.Client.ReactionRemoved += this.OnReactionRemoved;
            }
        }

        // TODO: Test these methods
        internal async Task HandleOnMessageReceived(SocketMessage message)
        {
            if (message.Content.TrimStart().StartsWith("!", StringComparison.InvariantCulture))
            {
                // Ignore commands
                this.Logger.Verbose("Message ignored because it's a command");
                return;
            }

            if (message.Author.Id == this.Client.CurrentUser.Id)
            {
                this.Logger.Verbose("Message ignored because it's from the bot");
                return;
            }

            // Only pay attention to messages in a guild channel
            if (!(message is IUserMessage userMessage &&
                userMessage.Channel is IGuildChannel guildChannel &&
                userMessage.Author is IGuildUser guildUser))
            {
                this.Logger.Verbose("Message ignored because it's not in a guild");
                return;
            }

            // TODO: See if there's a cheaper way to check if we have a current tournament without making it public.
            // This is a read-only lock, so it shouldn't be too bad, but it'll be blocked during write operations.
            // Don't use the helper method because we don't want to message the user each time if there's no tournament.
            // We also want to split this check and the TD check because we don't need to get the Discord member for
            // this check.
            TournamentsManager manager = this.GetTournamentsManager(guildChannel.Guild);
            Result<bool> currentTournamentExists = manager.TryReadActionOnCurrentTournament(currentTournament => true);
            if (!currentTournamentExists.Success)
            {
                this.Logger.Verbose("Message ignored because no current tournament is running");
                return;
            }

            // We want to do the basic access checks before using the more expensive write lock.

            Result<bool> hasDirectorPrivileges = manager.TryReadActionOnCurrentTournament(
                currentTournament => HasTournamentDirectorPrivileges(currentTournament, guildChannel, guildUser));
            if (!(hasDirectorPrivileges.Success && hasDirectorPrivileges.Value))
            {
                this.Logger.Verbose(
                    "Message ignored because user {id} does not have tournament director privileges", guildUser.Id);
                return;
            }

            await manager.DoReadWriteActionOnCurrentTournamentForMemberAsync(
                guildUser,
                async currentTournament =>
                {
                    // TODO: We need to have locks on these. Need to check the stages between locks, and ignore the message if
                    // it changes.
                    // Issue is that we should really rely on a command for this case. Wouldn't quite work with locks.
                    // But that would be something like !start, which would stop the rest of the interaction.
                    switch (currentTournament.Stage)
                    {
                        case TournamentStage.AddReaders:
                            await this.HandleAddReadersStage(currentTournament, guildChannel.Guild, message);
                            break;
                        case TournamentStage.SetRoundRobins:
                            await this.HandleSetRoundRobinsStage(currentTournament, message);
                            break;
                        case TournamentStage.AddTeams:
                            await this.HandleAddTeamsStage(currentTournament, message);
                            break;
                        case TournamentStage.Rebracketing:
                            await this.HandleRebracketingStage(currentTournament, guildChannel.Guild, message);
                            break;
                        default:
                            this.Logger.Verbose(
                                "Message ignored because it is during the stage {stage}", currentTournament.Stage);
                            break;
                    }
                });
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Event handler signature")]
        [SuppressMessage("Style", "CA1801:Remove unused parameter", Justification = "Event handler signature")]
        internal async Task HandleOnReactionAdded(
            Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel messageChannel, SocketReaction reaction)
        {
            // TODO: Combine checks for reaction add/remove handler into one method
            if (!cachedMessage.HasValue || reaction.UserId == this.Client.CurrentUser.Id)
            {
                // Ignore the bot's own additions. 
                return;
            }

            if (!(reaction.Channel is IGuildChannel guildChannel))
            {
                // Only listen to guild reacts.
                this.Logger.Verbose("Reaction addition ignored because it wasn't on a guild channel");
                return;
            }

            if (!(reaction.User.IsSpecified && reaction.User.Value is IGuildUser guildUser))
            {
                this.Logger.Verbose("Reaction addition ignored because it wasn't by a guild user");
                return;
            }

            IUserMessage message = cachedMessage.Value;
            Player player = null;
            bool playerAdded = false;
            string errorMessage = null;
            bool attempt = this
                .GetTournamentsManager(guildChannel.Guild)
                .TryReadWriteActionOnCurrentTournament(currentTournament =>
                {
                    player = this.GetPlayerFromReactionEventOrNull(
                        currentTournament, guildUser, message.Id, reaction.Emote.Name, out errorMessage);
                    if (player == null)
                    {
                        // TODO: we may want to remove the reaction if it's on our team-join messages.
                        this.Logger.Verbose(
                            "Reaction addition ignored because it wasn't by a player. Message: {errorMessage}",
                            errorMessage);
                        return;
                    }

                    // Because player equality/hashing is only based on the ID, we can check if the player is in the set with
                    // the new instance.
                    playerAdded = currentTournament.TryAddPlayer(player);
                });

            if (!(attempt && playerAdded) && errorMessage != null)
            {
                // TODO: We should remove the reaction they gave instead of this one (this may also require the manage
                // emojis permission?). This would also require a map from userIds/Players to emojis in the tournament
                // state. The Reactions collection doesn't have information on who added it, and iterating through each
                // emoji to see if the user was there would be slow.
                Task deleteReactionTask = message.RemoveReactionAsync(
                    reaction.Emote, guildUser, RequestOptionsSettings.Default);
                Task sendMessageTask = guildUser.SendMessageAsync(errorMessage);
                await Task.WhenAll(deleteReactionTask, sendMessageTask);
                this.Logger.Verbose("Reaction removed. Message: {errorMessage}", errorMessage);
                return;
            }
            else if (player != null)
            {
                this.Logger.Debug("Player {id} joined team {name}", player.Id, player.Team.Name);
                await guildUser.SendMessageAsync(BotStrings.YouHaveJoinedTeam(player.Team.Name));
            }
        }

        [SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Event handler signature")]
        [SuppressMessage("Style", "CA1801:Remove unused parameter", Justification = "Event handler signature")]
        internal async Task HandleOnReactionRemoved(
            Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel messageChannel, SocketReaction reaction)
        {
            if (!cachedMessage.HasValue || reaction.UserId == this.Client.CurrentUser.Id)
            {
                // Ignore the bot's own additions. 
                return;
            }

            if (!(reaction.Channel is IGuildChannel guildChannel))
            {
                // Only listen to guild reacts.
                this.Logger.Verbose("Reaction removal ignored because it wasn't on a guild channel");
                return;
            }

            if (!(reaction.User.IsSpecified && reaction.User.Value is IGuildUser guildUser))
            {
                this.Logger.Verbose("Reaction removal ignored because it wasn't by a guild user");
                return;
            }

            // Issue: when we remove a reaction, this will remove the team they were on. How would we know this, outside
            // of keeping state on this?
            // When the user chooses two emojis, and the bot removes the second one, the event handler is called. We
            // need to make sure that the user initiated this removal, which we can do by making sure that the player
            // we're removing from the set has the same team as the emoji maps to.
            // TODO: We may want to make this a dictionary of IDs to Players to make this operation efficient. We could
            // use ContainsKey to do the contains checks efficiently.
            await this.GetTournamentsManager(guildChannel.Guild).DoReadWriteActionOnCurrentTournamentForMemberAsync(
                guildUser,
                async currentTournament =>
                {
                    Player player = this.GetPlayerFromReactionEventOrNull(
                        currentTournament, guildUser, cachedMessage.Id, reaction.Emote.Name, out string errorMessage);
                    if (player == null)
                    {
                        // User should have already received an error message from the add if it was relevant.
                        this.Logger.Verbose(
                            "Reaction addition ignored because it wasn't by a player. Message: {errorMessage}",
                            errorMessage);
                        return;
                    }

                    if (currentTournament.TryGetPlayerTeam(guildUser.Id, out Team storedTeam) &&
                        storedTeam == player.Team &&
                        currentTournament.TryRemovePlayer(guildUser.Id))
                    {
                        this.Logger.Debug("Player {id} left team {name}", player.Id, player.Team.Name);
                        await guildUser.SendMessageAsync(
                            BotStrings.YouHaveLeftTeam(player.Team.Name), options: RequestOptionsSettings.Default);
                    }
                });
        }

        private Task HandleEvent(Func<Task> handler)
        {
            // Discord.Net complains if a task takes too long while handling the command. Since the current tournament
            // is protected by a mutex, it may take a long time to get access to it. As a result, we run the event
            // handler in a separate thread.
            Task.Run(async () =>
            {
                try
                {
                    await handler();
                }
                catch (Exception e)
                {
                    this.Logger.Error(e, "Exception when handling events");
                    throw;
                }
            });

            // If we return the task created by Task.Run the event handler will still be blocked. It seems like
            // Discord.Net will wait for the returned task to complete, which will block the Discord.Net's event
            // handler for too long. This does mean that we never know when an event is truly handled. This also
            // means that any data structures commands modify need to be thread-safe.
            return Task.CompletedTask;
        }

        private static bool HasTournamentDirectorPrivileges(
            IReadOnlyTournamentState currentTournament, IGuildChannel channel, IGuildUser user)
        {
            // TD is only allowed to run commands when they are a director of the current tournament.
            return currentTournament.GuildId == channel.GuildId &&
                (IsAdminUser(channel, user) || currentTournament.IsDirector(user.Id));
        }

        private static bool IsAdminUser(IGuildChannel channel, IGuildUser user)
        {
            return user.Id == channel.Guild.OwnerId || user.GuildPermissions.Administrator;
        }

        private static bool TryGetEmojis(int count, out IEmote[] emotes, out string errorMessage)
        {
            emotes = null;
            errorMessage = null;

            const int emojiLimit = 9 + 26;
            if (count < 0)
            {
                errorMessage = BotStrings.NumberOfTeamsMustBeGreaterThanZero;
                return false;
            }
            else if (count > emojiLimit)
            {
                errorMessage = BotStrings.TooManyTeams(emojiLimit);
                return false;
            }

            emotes = new IEmote[count];
            int numberLoopLimit = Math.Min(9, count);

            // Encoding.Unicode.GetString(new byte[] { 49, 0, 227, 32 }) to Encoding.Unicode.GetString(new byte[] { 58, 0, 227, 32 }) is 1-9
            byte[] numberBlock = { 49, 0, 227, 32 };
            int i = 0;
            for (; i < numberLoopLimit; i++)
            {
                emotes[i] = new Emoji(Encoding.Unicode.GetString(numberBlock));
                numberBlock[0] += 1;
            }

            // Encoding.Unicode.GetString(new byte[] { 60, 216, 230, 221 }) to Encoding.Unicode.GetString(new byte[] { 60, 216, 255, 221 }) is A-Z
            byte[] letterBlock = new byte[] { 60, 216, 230, 221 };
            for (; i < count; i++)
            {
                emotes[i] = new Emoji(Encoding.Unicode.GetString(letterBlock));
                letterBlock[2] += 1;
            }

            return true;
        }

        private static async Task AddReactionsToMessage(
            IUserMessage message, IEnumerable<IEmote> emotesForMessage, ITournamentState currentTournament)
        {
            currentTournament.AddJoinTeamMessageId(message.Id);
            await message.AddReactionsAsync(emotesForMessage.ToArray(), RequestOptionsSettings.Default);
        }

        private static int GetMaximumTeamCount(IReadOnlyTournamentState currentTournament)
        {
            return currentTournament.Readers.Count() * 2 + 1;
        }

        private Player GetPlayerFromReactionEventOrNull(
            IReadOnlyTournamentState currentTournament,
            IGuildUser user,
            ulong messageId,
            string emojiName,
            out string errorMessage)
        {
            errorMessage = null;

            if (currentTournament == null ||
                !currentTournament.IsJoinTeamMessage(messageId) ||
                !currentTournament.TryGetTeamFromSymbol(emojiName, out Team team))
            {
                // Ignore reactions added/removed from non-team messages.
                return null;
            }

            // TODO: since we have the message ID it's unlikely we need to verify the guild, but we should double check.
            if (user.Id == this.Client.CurrentUser.Id)
            {
                errorMessage = BotStrings.BotCannotJoinAsPlayer;
                return null;
            }

            ulong userId = user.Id;
            if (currentTournament.IsDirector(userId))
            {
                errorMessage = BotStrings.TournamentDirectorCannotJoinAsPlayer;
                return null;
            }

            if (currentTournament.IsReader(userId))
            {
                errorMessage = BotStrings.ReaderCannotJoinAsPlayer;
                return null;
            }

            Player player = new Player()
            {
                Id = userId,
                Team = team
            };
            return player;
        }

        private async Task HandleAddReadersStage(ITournamentState currentTournament, IGuild guild, SocketMessage message)
        {
            IEnumerable<Task<IGuildUser>> getReaderMembers = message.MentionedUsers
                .Select(user => guild.GetUserAsync(user.Id, options: RequestOptionsSettings.Default));
            IGuildUser[] readerMembers = await Task.WhenAll(getReaderMembers);
            IEnumerable<Reader> readers = readerMembers.Select(member => new Reader()
            {
                Id = member.Id,
                Name = member.Nickname ?? member.Username
            });

            currentTournament.AddReaders(readers);
            if (!currentTournament.Readers.Any())
            {
                this.Logger.Debug("No readers specified, so staying in the AddReaders stage");
                await message.Channel.SendMessageAsync(
                    BotStrings.NoReadersAddedMinimumReaderCount, options: RequestOptionsSettings.Default);
                return;
            }

            await message.Channel.SendMessageAsync(
                BotStrings.ReadersTotalForTournament(currentTournament.Readers.Count()));
            await this.UpdateStage(currentTournament, TournamentStage.SetRoundRobins, message.Channel);
        }

        private async Task HandleSetRoundRobinsStage(ITournamentState currentTournament, SocketMessage message)
        {
            if (!int.TryParse(message.Content, out int rounds))
            {
                this.Logger.Debug("Round robin count specified couldn't be parsed as an int");
                return;
            }
            else if (rounds <= 0 || rounds > TournamentState.MaxRoundRobins)
            {
                this.Logger.Debug("Round robin count ({0}) specified is invalid", rounds);
                await message.Channel.SendMessageAsync(
                    BotStrings.InvalidNumberOfRoundRobins(TournamentState.MaxRoundRobins),
                    options: RequestOptionsSettings.Default);
                return;
            }

            currentTournament.RoundRobinsCount = rounds;
            await this.UpdateStage(currentTournament, TournamentStage.AddTeams, message.Channel);
        }

        // TODO: Make string message and IGuildChannel?
        private async Task HandleAddTeamsStage(ITournamentState currentTournament, SocketMessage message)
        {
            if (!TeamsParser.TryParseTeams(message.Content, out IEnumerable<Team> teams, out string errorMessage))
            {
                await message.Channel.SendMessageAsync(errorMessage, options: RequestOptionsSettings.Default);
                this.Logger.Debug("Team names could not be parsed. Error message: {errorMessage}", errorMessage);
                return;
            }

            currentTournament.AddTeams(teams);

            int teamsCount = currentTournament.Teams.Count();
            if (teamsCount < 2)
            {
                await message.Channel.SendMessageAsync(
                    BotStrings.MustBeTwoTeamsPerTournament, options: RequestOptionsSettings.Default);
                currentTournament.RemoveTeams(teams);
                this.Logger.Debug("Too few teams specified in AddTeams stage ({0})", teamsCount);
                return;
            }

            int maxTeamsCount = GetMaximumTeamCount(currentTournament);
            if (teamsCount > maxTeamsCount)
            {
                currentTournament.TryClearTeams();
                await message.Channel.SendMessageAsync(
                    BotStrings.TooManyTeams(maxTeamsCount), options: RequestOptionsSettings.Default);
                this.Logger.Debug("Too many teams specified in AddTeams stage ({0})", teamsCount);
                return;
            }

            if (!TryGetEmojis(teamsCount, out IEmote[] emotes, out errorMessage))
            {
                // Something very strange has happened. Undo the addition and tell the user.
                currentTournament.TryClearTeams();
                await message.Channel.SendMessageAsync(
                    BotStrings.UnexpectedErrorAddingTeams(errorMessage), options: RequestOptionsSettings.Default);
                this.Logger.Debug("Couldn't get emojis in AddTeams stage. Error message: {errorMessage}", errorMessage);
                return;
            }

            await this.UpdateStage(currentTournament, TournamentStage.AddPlayers, message.Channel);

            currentTournament.ClearSymbolsToTeam();
            Debug.Assert(
                emotes.Length == teamsCount,
                $"Teams ({teamsCount}) and emojis ({emotes.Length}) lengths are unequal.");

            // There are limits to the number of fields in an embed and the number of reactions to a message.
            // They are 25 and 20, respectively. Limit the number of teams per message to this limit.
            // maxFieldSize is an inclusive limit, so we need to include the - 1 to make add a new slot only
            // when that limit is exceeded.

            List<Task> addReactionsTasks = new List<Task>();
            int emojiIndexForFields = 0;
            int emojiIndexForMessages = 0;

            await message.Channel.SendAllEmbeds(
                currentTournament.Teams,
                () => new EmbedBuilder
                {
                    Title = BotStrings.JoinTeams,
                    Description = BotStrings.ClickOnReactionsJoinTeam
                },
                (team, teamIndex) =>
                {
                    IEmote emote = emotes[emojiIndexForFields];
                    emojiIndexForFields++;

                    currentTournament.AddSymbolToTeam(emote.Name, team);

                    return new EmbedFieldBuilder()
                    {
                        Name = emote.Name,
                        Value = team.Name
                    };
                },
                (userMessage, embed) =>
                {
                    IEmote[] messageEmotes = new IEmote[embed.Fields.Length];
                    for (int i = 0; i < messageEmotes.Length; i++)
                    {
                        messageEmotes[i] = emotes[emojiIndexForMessages];
                        emojiIndexForMessages++;
                    }

                    currentTournament.AddJoinTeamMessageId(userMessage.Id);
                    addReactionsTasks.Add(AddReactionsToMessage(userMessage, messageEmotes, currentTournament));
                });

            await Task.WhenAll(addReactionsTasks);
            this.Logger.Debug("All reactions added for add players stage");
        }

        private async Task HandleRebracketingStage(
            ITournamentState currentTournament, IGuild guild, SocketMessage message)
        {
            if (!TeamsParser.TryParseTeams(message.Content, out IEnumerable<Team> teams, out string errorMessage))
            {
                await message.Channel.SendMessageAsync(errorMessage, options: RequestOptionsSettings.Default);
                this.Logger.Debug("Team names could not be parsed. Error message: {errorMessage}", errorMessage);
                return;
            }

            // There should be no new teams in the rebracket, although teams can drop out
            IEnumerable<Team> newTeams = teams.Except(currentTournament.Teams);
            if (newTeams.Any())
            {
                IEnumerable<string> newTeamNames = newTeams.Select(team => team.Name);
                await message.Channel.SendMessageAsync(
                    BotStrings.CannotNewTeamsAddDuringRebracket(newTeamNames),
                    options: RequestOptionsSettings.Default);
                return;
            }

            // TODO: Figure out if we want the number of round robins in the rebracket to be specified. Generally for
            // a rebracketed bracket, it's 1
            IScheduleFactory scheduleFactory = new RoundRobinScheduleFactory(roundRobins: 1);

            Schedule bracketSchedule = scheduleFactory.Generate(
                new HashSet<Team>(teams),
                new HashSet<Reader>(currentTournament.Readers));

            int oldRoundCount = currentTournament.Schedule.Rounds.Count;
            foreach (Round round in bracketSchedule.Rounds)
            {
                currentTournament.Schedule.AddRound(round);
            }

            ITournamentChannelManager channelManager = new TournamentChannelManager(guild);
            await channelManager.CreateChannelsForRebracket(
                this.Client.CurrentUser, currentTournament, bracketSchedule.Rounds, oldRoundCount + 1);

            await this.UpdateStage(currentTournament, TournamentStage.RunningTournament, message.Channel);
        }

        private Task OnMessageReceived(SocketMessage message)
        {
            return this.HandleEvent(() => this.HandleOnMessageReceived(message));
        }

        private Task OnReactionAdded(
            Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel messageChannel, SocketReaction reaction)
        {
            return this.HandleEvent(() => this.HandleOnReactionAdded(cachedMessage, messageChannel, reaction));
        }

        private Task OnReactionRemoved(
            Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel messageChannel, SocketReaction reaction)
        {
            return this.HandleEvent(() => this.HandleOnReactionRemoved(cachedMessage, messageChannel, reaction));
        }

        private async Task UpdateStage(ITournamentState tournament, TournamentStage stage, IMessageChannel channel)
        {
            tournament.UpdateStage(stage, out string title, out string instructions);
            if (title == null && instructions == null)
            {
                return;
            }

            EmbedBuilder addTeamsEmbedBuilder = new EmbedBuilder
            {
                Title = title,
                Description = instructions
            };
            await channel.SendMessageAsync(
                embed: addTeamsEmbedBuilder.Build(), options: RequestOptionsSettings.Default);
            this.Logger.Debug("Moved to stage {stage}", stage);
        }

        private static TournamentsManager CreateTournamentsManager(ulong id)
        {
            TournamentsManager manager = new TournamentsManager
            {
                GuildId = id
            };
            return manager;
        }

        private TournamentsManager GetTournamentsManager(IGuild guild)
        {
            return this.GlobalManager.GetOrAdd(guild.Id, CreateTournamentsManager);
        }
    }
}
