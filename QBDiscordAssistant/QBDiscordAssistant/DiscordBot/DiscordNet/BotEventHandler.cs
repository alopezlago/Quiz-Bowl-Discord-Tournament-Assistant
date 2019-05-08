using Discord;
using Discord.WebSocket;
using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class BotEventHandler : IDisposable
    {
        private const int TeamCountLimit = 9 + 26;
        private const int ReaderCountLimit = TeamCountLimit / 2;
        private const int MaxTeamsInMessage = 20;

        // TODO: Add wrapper class/interface for the client to let us test the event handlers.
        // The events themselves pass in SocketMessages, so perhaps grab some fields and abstract the logic. Let's do a
        // straight transfer first.
        public BotEventHandler(DiscordSocketClient client, GlobalTournamentsManager globalManager)
        {
            this.IsDisposed = false;
            this.Client = client;
            this.GlobalManager = globalManager;

            this.Client.MessageReceived += this.OnMessageReceived;
            this.Client.ReactionAdded += this.OnReactionAdded;
            this.Client.ReactionRemoved += this.OnReactionRemoved;
        }

        // TODO: Add wrapper class/interface for the client to let us test the event handlers.
        private BaseSocketClient Client { get; }

        private bool IsDisposed { get; set; }

        private GlobalTournamentsManager GlobalManager { get; }

        public void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.IsDisposed = true;
                this.Client.MessageReceived += this.OnMessageReceived;
                this.Client.ReactionAdded += this.OnReactionAdded;
                this.Client.ReactionRemoved += this.OnReactionRemoved;
            }
        }

        // TODO: Test these methods
        internal async Task HandleOnMessageReceived(SocketMessage message)
        {
            if (message.Content.TrimStart().StartsWith("!"))
            {
                // Ignore commands
                return;
            }

            if (message.Author.Id == this.Client.CurrentUser.Id)
            {
                return;
            }

            // Only pay attention to messages in a guild channel
            if (!(message is IUserMessage userMessage &&
                userMessage.Channel is IGuildChannel guildChannel &&
                userMessage.Author is IGuildUser guildUser))
            {
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
                return;
            }

            // We want to do the basic access checks before using the more expensive write lock.

            Result<bool> hasDirectorPrivileges = manager.TryReadActionOnCurrentTournament(
                currentTournament => HasTournamentDirectorPrivileges(currentTournament, guildChannel, guildUser));
            if (!(hasDirectorPrivileges.Success && hasDirectorPrivileges.Value))
            {
                return;
            }

            await manager.DoReadWriteActionOnCurrentTournamentForMember(
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
                        default:
                            break;
                    }
                });
        }

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
                return;
            }

            if (!(reaction.User.IsSpecified && reaction.User.Value is IGuildUser guildUser))
            {
                return;
            }

            IUserMessage message = cachedMessage.Value;
            Player player = null;
            bool playerAdded = false;
            bool attempt = this
                .GetTournamentsManager(guildChannel.Guild)
                .TryReadWriteActionOnCurrentTournament(currentTournament =>
                {
                    player = GetPlayerFromReactionEventOrNull(
                        currentTournament, guildUser, message.Id, reaction.Emote.Name);
                    if (player == null)
                    {
                        // TODO: we may want to remove the reaction if it's on our team-join messages.
                        return;
                    }

                    // Because player equality/hashing is only based on the ID, we can check if the player is in the set with
                    // the new instance.
                    playerAdded = currentTournament.TryAddPlayer(player);
                });

            if (!(attempt && playerAdded))
            {
                // TODO: We should remove the reaction they gave instead of this one (this may also require the manage
                // emojis permission?). This would also require a map from userIds/Players to emojis in the tournament
                // state. The Reactions collection doesn't have information on who added it, and iterating through each
                // emoji to see if the user was there would be slow.
                Task deleteReactionTask = message.RemoveReactionAsync(reaction.Emote, guildUser);
                string errorMessage = attempt ?
                    "You are already on a team. Click on the emoji of the team you were on to leave that team, then click on the emoji of the team you want to join." :
                    "We were unable to add you to the team. Try again.";
                Task sendMessageTask = guildUser.SendMessageAsync(errorMessage);
                await Task.WhenAll(deleteReactionTask, sendMessageTask);
                return;
            }
            else if (player != null)
            {
                await guildUser.SendMessageAsync($"You have joined the team {player.Team.Name}");
            }
        }

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
                return;
            }

            if (!(reaction.User.IsSpecified && reaction.User.Value is IGuildUser guildUser))
            {
                return;
            }

            // Issue: when we remove a reaction, this will remove the team they were on. How would we know this, outside
            // of keeping state on this?
            // When the user chooses two emojis, and the bot removes the second one, the event handler is called. We
            // need to make sure that the user initiated this removal, which we can do by making sure that the player
            // we're removing from the set has the same team as the emoji maps to.
            // TODO: We may want to make this a dictionary of IDs to Players to make this operation efficient. We could
            // use ContainsKey to do the contains checks efficiently.
            await this.GetTournamentsManager(guildChannel.Guild).DoReadWriteActionOnCurrentTournamentForMember(
                guildUser,
                async currentTournament =>
                {
                    Player player = GetPlayerFromReactionEventOrNull(
                        currentTournament, guildUser, cachedMessage.Id, reaction.Emote.Name);
                    if (player == null)
                    {
                        return;
                    }

                    if (currentTournament.TryGetPlayerTeam(guildUser.Id, out Team storedTeam) &&
                        storedTeam == player.Team &&
                        currentTournament.TryRemovePlayer(guildUser.Id))
                    {
                        await guildUser.SendMessageAsync($"You have left the team {player.Team.Name}");
                    }
                });
        }

        private static Task HandleEvent(Func<Task> handler)
        {
            // Discord.Net complains if a task takes too long while handling the command. Since the current tournament
            // is protected by a mutex, it may take a long time to get access to it. As a result, we run the event
            // handler in a separate thread.
            Task.Run(async () => await handler());

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
            // TODO: Verify if this does work for non-owner admins, since they might not have overwritten Equals.
            return user.Id == channel.Guild.OwnerId || user.GuildPermissions.Equals(GuildPermissions.All);
        }

        private static bool TryGetEmojis(
            BaseSocketClient client, int count, out IEmote[] emotes, out string errorMessage)
        {
            emotes = null;
            errorMessage = null;

            const int emojiLimit = 9 + 26;
            if (count < 0)
            {
                errorMessage = "Number of teams must be greater than 0.";
                return false;
            }
            else if (count > emojiLimit)
            {
                errorMessage = $"Too many teams. Maximum number of teams: {emojiLimit}";
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

        private async Task AddReactionsToMessage(
            IUserMessage message, IEnumerable<IEmote> emotesForMessage, ITournamentState currentTournament)
        {
            currentTournament.AddJoinTeamMessageId(message.Id);
            await message.AddReactionsAsync(emotesForMessage.ToArray());
        }

        private int GetMaximumTeamCount(IReadOnlyTournamentState currentTournament)
        {
            return currentTournament.Readers.Count() * 2 + 1;
        }

        private Player GetPlayerFromReactionEventOrNull(
            IReadOnlyTournamentState currentTournament, IGuildUser user, ulong messageId, string emojiName)
        {
            // TODO: since we have the message ID it's unlikely we need to verify the guild, but we should double check.
            if (user.Id == this.Client.CurrentUser.Id ||
                currentTournament == null ||
                !currentTournament.IsJoinTeamMessage(messageId) ||
                !currentTournament.TryGetTeamFromSymbol(emojiName, out Team team))
            {
                return null;
            }

            ulong userId = user.Id;
            if (currentTournament.IsDirector(userId))
            {
                return null;
            }

            if (currentTournament.IsReader(userId))
            {
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
                .Select(user => guild.GetUserAsync(user.Id));
            IGuildUser[] readerMembers = await Task.WhenAll(getReaderMembers);
            IEnumerable<Reader> readers = readerMembers.Select(member => new Reader()
            {
                Id = member.Id,
                Name = member.Nickname ?? member.Username
            });

            currentTournament.AddReaders(readers);
            if (!currentTournament.Readers.Any())
            {
                await message.Channel.SendMessageAsync("No readers added. There must be at least one reader for a tournament.");
                return;
            }

            await message.Channel.SendMessageAsync(
                $"{currentTournament.Readers.Count()} readers total for the tournament.");
            await this.UpdateStage(currentTournament, TournamentStage.SetRoundRobins, message.Channel);
        }

        private async Task HandleSetRoundRobinsStage(ITournamentState currentTournament, SocketMessage message)
        {
            if (!int.TryParse(message.Content, out int rounds))
            {
                return;
            }
            else if (rounds <= 0 || rounds > TournamentState.MaxRoundRobins)
            {
                await message.Channel.SendMessageAsync($"Invalid number of round robins. The number must be between 1 and {TournamentState.MaxRoundRobins}");
                return;
            }

            currentTournament.RoundRobinsCount = rounds;
            await this.UpdateStage(currentTournament, TournamentStage.AddTeams, message.Channel);
        }

        // TODO: Make string message and IGuildChannel?
        private async Task HandleAddTeamsStage(ITournamentState currentTournament, SocketMessage message)
        {
            // We don't use Environment.NewLine because we only need a plain newline, even in Windows (shift+Enter)
            string[] teamsList = message.Content.Split("\n");
            List<Team> teams = new List<Team>();
            string errorMessage;
            for (int i  = 0; i < teamsList.Length; i++)
            {
                string teamList = teamsList[i];
                if (!TeamNameParser.TryGetTeamNamesFromParts(
                    teamList, out IList<string> newTeamNames, out errorMessage))
                {
                    await message.Channel.SendMessageAsync(errorMessage);
                    return;
                }

                teams.AddRange(newTeamNames.Select(teamName =>
                    new Team()
                    {
                        Name = teamName,
                        Bracket = i
                    }));
            }

            currentTournament.AddTeams(teams);

            int teamsCount = currentTournament.Teams.Count();
            if (teamsCount < 2)
            {
                await message.Channel.SendMessageAsync("There must be at least two teams for a tournament. Specify more teams.");
                currentTournament.RemoveTeams(teams);
                return;
            }

            int maxTeamsCount = this.GetMaximumTeamCount(currentTournament);
            if (teamsCount > maxTeamsCount)
            {
                currentTournament.TryClearTeams();
                await message.Channel.SendMessageAsync(
                    $"There are too many teams. This bot can only handle {maxTeamsCount}-team tournaments. None of the teams have been added.");
                return;
            }

            if (!TryGetEmojis(this.Client, teamsCount, out IEmote[] emotes, out errorMessage))
            {
                // Something very strange has happened. Undo the addition and tell the user.
                currentTournament.TryClearTeams();
                await message.Channel.SendMessageAsync(
                    $"Unexpected failure adding teams: '{errorMessage}'. None of the teams have been added.");
                return;
            }

            await this.UpdateStage(currentTournament, TournamentStage.AddPlayers, message.Channel);

            currentTournament.ClearSymbolsToTeam();
            int emojiIndex = 0;
            Debug.Assert(
                emotes.Length == teamsCount,
                $"Teams ({teamsCount}) and emojis ({emotes.Length}) lengths are unequal.");

            // There are limits to the number of fields in an embed and the number of reactions to a message.
            // They are 25 and 20, respectively. Limit the number of teams per message to this limit.
            // maxFieldSize is an inclusive limit, so we need to include the - 1 to make add a new slot only
            // when that limit is exceeded.
            int addPlayersEmbedsCount = 1 + (teamsCount - 1) / MaxTeamsInMessage;
            IEnumerator<Team> teamsEnumerator = currentTournament.Teams.GetEnumerator();
            List<Task> addReactionsTasks = new List<Task>();
            for (int i = 0; i < addPlayersEmbedsCount; i++)
            {
                EmbedBuilder embedBuilder = new EmbedBuilder();
                embedBuilder.Title = "Join Teams";
                embedBuilder.Description = "Click on the reactions corresponding to your team to join it.";
                int fieldCount = 0;
                List<IEmote> emotesForMessage = new List<IEmote>();
                while (fieldCount < MaxTeamsInMessage && teamsEnumerator.MoveNext())
                {
                    IEmote emote = emotes[emojiIndex];
                    emotesForMessage.Add(emote);
                    Team team = teamsEnumerator.Current;
                    embedBuilder.AddField(emote.Name, team.Name);
                    currentTournament.AddSymbolToTeam(emote.Name, team);

                    fieldCount++;
                    emojiIndex++;
                }

                // We should generally avoid await inside of loops, but we want the messages and emojis to be
                // in order.
                IUserMessage newMessage = await message.Channel.SendMessageAsync(embed: embedBuilder.Build());
                currentTournament.AddJoinTeamMessageId(newMessage.Id);
                addReactionsTasks.Add(this.AddReactionsToMessage(newMessage, emotesForMessage, currentTournament));
            }

            await Task.WhenAll(addReactionsTasks);
        }

        private Task OnMessageReceived(SocketMessage message)
        {
            return HandleEvent(() => HandleOnMessageReceived(message));
        }

        private Task OnReactionAdded(
            Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel messageChannel, SocketReaction reaction)
        {
            return HandleEvent(() => HandleOnReactionAdded(cachedMessage, messageChannel, reaction));
        }

        private Task OnReactionRemoved(
            Cacheable<IUserMessage, ulong> cachedMessage, ISocketMessageChannel messageChannel, SocketReaction reaction)
        {
            return HandleEvent(() => HandleOnReactionRemoved(cachedMessage, messageChannel, reaction));
        }

        private async Task UpdateStage(ITournamentState tournament, TournamentStage stage, IMessageChannel channel)
        {
            tournament.UpdateStage(stage, out string title, out string instructions);
            if (title == null && instructions == null)
            {
                return;
            }

            EmbedBuilder addTeamsEmbedBuilder = new EmbedBuilder();
            addTeamsEmbedBuilder.Title = title;
            addTeamsEmbedBuilder.Description = instructions;
            await channel.SendMessageAsync(embed: addTeamsEmbedBuilder.Build());
        }

        private static TournamentsManager CreateTournamentsManager(ulong id)
        {
            TournamentsManager manager = new TournamentsManager();
            manager.GuildId = id;
            return manager;
        }

        private TournamentsManager GetTournamentsManager(IGuild guild)
        {
            return this.GlobalManager.GetOrAdd(guild.Id, CreateTournamentsManager);
        }
    }
}
