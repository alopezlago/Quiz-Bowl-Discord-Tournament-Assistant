using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using QBDiscordAssistant.Tournament;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QBDiscordAssistant.Discord
{
    public class BotEventHandler : IDisposable
    {
        private const int TeamCountLimit = 9 + 26;
        private const int ReaderCountLimit = TeamCountLimit / 2;
        private const int MaxTeamsInMessage = 20;

        private readonly DiscordClient client;
        private readonly TournamentsManager manager;

        public BotEventHandler(DiscordClient client, TournamentsManager manager)
        {
            this.IsDisposed = false;
            this.client = client;
            this.manager = manager;

            this.client.MessageCreated += this.OnMessageCreated;
            this.client.MessageReactionAdded += this.OnReactionAdded;
            this.client.MessageReactionRemoved += this.OnReactionRemoved;
        }

        private bool IsDisposed { get; set; }

        public void Dispose()
        {
            if (!this.IsDisposed)
            {
                this.IsDisposed = true;
                this.client.MessageCreated -= this.OnMessageCreated;
                this.client.MessageReactionAdded -= this.OnReactionAdded;
                this.client.MessageReactionRemoved -= this.OnReactionRemoved;
            }
        }

        public async Task OnReactionAdded(MessageReactionAddEventArgs args)
        {
            Player player = GetPlayerFromReactionEventOrNull(args.User, args.Message.Id, args.Emoji.Name);
            if (player == null)
            {
                return;
            }

            DiscordMember member = await args.Channel.Guild.GetMemberAsync(args.User.Id);

            // Because player equality/hashing is only based on the ID, we can check if the player is in the set with
            // the new instance.
            if (!this.manager.CurrentTournament.TryAddPlayer(player))
            {
                // TODO: We should remove the reaction they gave instead of this one (this may also require the manage
                // emojis permission?). This would also require a map from userIds/Players to emojis in the tournament
                // state. The Reactions collection doesn't have information on who added it, and iterating through each
                // emoji to see if the user was there would be slow.
                Task deleteReactionTask = args.Message.DeleteReactionAsync(args.Emoji, args.User);
                Task sendMessageTask = member.SendMessageAsync(
                    "You are already on a team. Click on the emoji of the team you were on to leave that team, then click on the emoji of the team you want to join.");
                await Task.WhenAll(deleteReactionTask, sendMessageTask);
                return;
            }

            await member.SendMessageAsync($"You have joined the team {player.Team.Name}");
        }

        public async Task OnReactionRemoved(MessageReactionRemoveEventArgs args)
        {
            Player player = GetPlayerFromReactionEventOrNull(args.User, args.Message.Id, args.Emoji.Name);
            if (player == null)
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
            if (this.manager.CurrentTournament.TryGetPlayerTeam(args.User.Id, out Team storedTeam) &&
                storedTeam == player.Team &&
                this.manager.CurrentTournament.TryRemovePlayer(args.User.Id))
            {
                DiscordMember member = await args.Channel.Guild.GetMemberAsync(args.User.Id);
                await member.SendMessageAsync($"You have left the team {player.Team.Name}");
            }
        }

        public async Task OnMessageCreated(MessageCreateEventArgs args)
        {
            if (args.Author.IsCurrent ||
                this.manager.CurrentTournament == null)
            {
                return;
            }

            if (args.Message.Content.TrimStart().StartsWith("!"))
            {
                // Ignore commands
                return;
            }

            DiscordMember member = await args.Channel.Guild.GetMemberAsync(args.Author.Id);
            if (!this.HasTournamentDirectorPrivileges(args.Channel, member))
            {
                return;
            }

            // TODO: We need to have locks on these. Need to check the stages between locks, and ignore the message if
            // it changes.
            // Issue is that we should really rely on a command for this case. Wouldn't quite work with locks.
            // But that would be something like !start, which would stop the rest of the interaction.
            switch (this.manager.CurrentTournament.Stage)
            {
                case TournamentStage.AddReaders:
                    await this.HandleAddReadersStage(args);
                    break;
                case TournamentStage.SetRoundRobins:
                    await this.HandleSetRoundRobinsStage(args);
                    break;
                case TournamentStage.AddTeams:
                    await this.HandleAddTeamsStage(args);
                    break;
                default:
                    break;
            }
        }

        private static bool IsAdminUser(DiscordChannel channel, DiscordMember member)
        {
            return member.IsOwner ||
                (channel.PermissionsFor(member) & Permissions.Administrator) == Permissions.Administrator;
        }

        private static bool TryGetEmojis(
            DiscordClient client, int count, out DiscordEmoji[] emojis, out string errorMessage)
        {
            emojis = null;
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

            emojis = new DiscordEmoji[count];
            int numberLoopLimit = Math.Min(9, count);

            // TODO: Figure out why the number block approach isn't working. It's not getting us the emoji. It should
            // be from byte[] { 49, 0, 15, 254 } to byte[] { 57, 0, 15, 254 }
            string[] symbols = new string[] { ":one:", ":two:", ":three:", ":four:", ":five:", ":six:", ":seven:", ":eight:", ":nine:" };
            int i = 0;
            for (; i < numberLoopLimit; i++)
            {
                emojis[i] = DiscordEmoji.FromName(client, symbols[i]);
            }

            // Encoding.Unicode.GetString(new byte[] { 60, 216, 230, 221 } to Encoding.Unicode.GetString(new byte[] { 60, 216, 255, 221 } is A-Z
            byte[] letterBlock = new byte[] { 60, 216, 230, 221 };
            for (; i < count; i++)
            {
                emojis[i] = DiscordEmoji.FromUnicode(Encoding.Unicode.GetString(letterBlock));
                letterBlock[2] += 1;
            }

            return true;
        }

        private bool HasTournamentDirectorPrivileges(DiscordChannel channel, DiscordMember member)
        {
            // TD is only allowed to run commands when they are a director of the current tournament.
            return this.manager.CurrentTournament != null &&
                manager.CurrentTournament.GuildId == channel.GuildId &&
                (IsAdminUser(channel, member) || manager.CurrentTournament.IsDirector(member.Id));
        }

        private async Task HandleAddReadersStage(MessageCreateEventArgs args)
        {
            IEnumerable<Task<DiscordMember>> getReaderMembers = args.MentionedUsers
                .Select(user => args.Guild.GetMemberAsync(user.Id));
            DiscordMember[] readerMembers = await Task.WhenAll(getReaderMembers);
            IEnumerable<Reader> readers = readerMembers.Select(member => new Reader()
            {
                Id = member.Id,
                Name = member.Nickname ?? member.DisplayName
            });

            // TODO: Need to determine if we want any messaging here
            if (!this.manager.CurrentTournament.TryAddReaders(readers))
            {
                DiscordMember member = await args.Guild.GetMemberAsync(args.Author.Id);
                await member.SendMessageAsync("Somebody else has already added readers.");
                return;
            }

            if (!this.manager.CurrentTournament.Readers.Any())
            {
                await args.Channel.SendMessageAsync("No readers added. There must be at least one reader for a tournament.");
                return;
            }

            await args.Channel.SendMessageAsync(
                $"{this.manager.CurrentTournament.Readers.Count()} readers total for the tournament.");
            await this.UpdateStage(args.Channel, TournamentStage.SetRoundRobins);
        }

        private async Task HandleSetRoundRobinsStage(MessageCreateEventArgs args)
        {
            if (!int.TryParse(args.Message.Content, out int rounds))
            {
                return;
            }
            else if (rounds <= 0 || rounds > TournamentState.MaxRoundRobins)
            {
                await args.Channel.SendMessageAsync($"Invalid number of round robins. The number must be between 1 and {TournamentState.MaxRoundRobins}");
                return;
            }

            this.manager.CurrentTournament.RoundRobinsCount = rounds;
            await this.UpdateStage(args.Channel, TournamentStage.AddTeams);
        }

        private async Task HandleAddTeamsStage(MessageCreateEventArgs args)
        {
            if (!TeamNameParser.TryGetTeamNamesFromParts(
                args.Message.Content, out HashSet<string> teamNames, out string errorMessage))
            {
                await args.Channel.SendMessageAsync(errorMessage);
                return;
            }

            IEnumerable<Team> newTeams = teamNames
                .Select(name => new Team()
                {
                    Name = name
                });
            if (!this.manager.CurrentTournament.TryAddTeams(newTeams))
            {
                DiscordMember member = await args.Guild.GetMemberAsync(args.Author.Id);
                await member.SendMessageAsync("Teams have already been added.");
                return;
            }

            int teamsCount = this.manager.CurrentTournament.Teams.Count();
            if (teamsCount < 2)
            {
                await args.Channel.SendMessageAsync("There must be at least two teams for a tournament. Specify more teams.");
                return;
            }

            int maxTeamsCount = this.GetMaximumTeamCount();
            if (teamsCount > maxTeamsCount)
            {
                this.manager.CurrentTournament.TryClearTeams();
                await args.Channel.SendMessageAsync(
                    $"There are too many teams. This bot can only handle {maxTeamsCount}-team tournaments. None of the teams have been added.");
                return;
            }

            if (!TryGetEmojis(this.client, teamsCount, out DiscordEmoji[] emojis, out errorMessage))
            {
                // Something very strange has happened. Undo the addition and tell the user.
                this.manager.CurrentTournament.TryClearTeams();
                await args.Channel.SendMessageAsync(
                    $"Unexpected failure adding teams: '{errorMessage}'. None of the teams have been added.");
                return;
            }

            await this.UpdateStage(args.Channel, TournamentStage.AddPlayers);

            this.manager.CurrentTournament.SymbolToTeam.Clear();
            int emojiIndex = 0;
            Debug.Assert(
                emojis.Length == teamsCount,
                $"Teams ({teamsCount}) and emojis ({emojis.Length}) lengths are unequal.");

            // There are limits to the number of fields in an embed and the number of reactions to a message.
            // They are 25 and 20, respectively. Limit the number of teams per message to this limit.
            // maxFieldSize is an inclusive limit, so we need to include the - 1 to make add a new slot only
            // when that limit is exceeded.
            int addPlayersEmbedsCount = 1 + (teamsCount - 1) / MaxTeamsInMessage;
            IEnumerator<Team> teamsEnumerator = this.manager.CurrentTournament.Teams.GetEnumerator();
            List<Task> addReactionsTasks = new List<Task>();
            for (int i = 0; i < addPlayersEmbedsCount; i++)
            {
                DiscordEmbedBuilder embedBuilder = new DiscordEmbedBuilder();
                embedBuilder.Title = "Join Teams";
                embedBuilder.Description = "Click on the reactions corresponding to your team to join it.";
                int fieldCount = 0;
                List<DiscordEmoji> emojisForMessage = new List<DiscordEmoji>();
                while (fieldCount < MaxTeamsInMessage && teamsEnumerator.MoveNext())
                {
                    DiscordEmoji emoji = emojis[emojiIndex];
                    emojisForMessage.Add(emoji);
                    Team team = teamsEnumerator.Current;
                    embedBuilder.AddField(emoji.Name, team.Name);
                    this.manager.CurrentTournament.SymbolToTeam.Add(emoji.Name, team);

                    fieldCount++;
                    emojiIndex++;
                }

                // We should generally avoid await inside of loops, but we want the messages and emojis to be
                // in order.
                DiscordMessage message = await args.Channel.SendMessageAsync(embed: embedBuilder.Build());
                this.manager.CurrentTournament.JoinTeamMessageIds.Add(message.Id);
                addReactionsTasks.Add(this.AddReactionsToMessage(message, emojisForMessage));
            }

            await Task.WhenAll(addReactionsTasks);
        }

        private async Task AddReactionsToMessage(DiscordMessage message, IEnumerable<DiscordEmoji> emojisForMessage)
        {
            this.manager.CurrentTournament.JoinTeamMessageIds.Add(message.Id);
            foreach (DiscordEmoji emoji in emojisForMessage)
            {
                await message.CreateReactionAsync(emoji);
            }
        }

        private int GetMaximumTeamCount()
        {
            return this.manager.CurrentTournament.Readers.Count() * 2 + 1;
        }

        private Player GetPlayerFromReactionEventOrNull(DiscordUser user, ulong messageId, string emojiName)
        {
            // TODO: since we have the message ID it's unlikely we need to verify the guild, but we should double check.
            if (user.IsCurrent ||
                this.manager.CurrentTournament == null ||
                !this.manager.CurrentTournament.JoinTeamMessageIds.Contains(messageId) ||
                !this.manager.CurrentTournament.SymbolToTeam.TryGetValue(emojiName, out Team team))
            {
                return null;
            }

            ulong userId = user.Id;
            if (this.manager.CurrentTournament.IsDirector(userId))
            {
                return null;
            }

            if (this.manager.CurrentTournament.IsReader(userId))
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

        private async Task UpdateStage(DiscordChannel channel, TournamentStage stage)
        {
            this.manager.CurrentTournament.UpdateStage(stage, out string title, out string instructions);
            if (title == null && instructions == null)
            {
                return;
            }

            DiscordEmbedBuilder addTeamsEmbedBuilder = new DiscordEmbedBuilder();
            addTeamsEmbedBuilder.Title = title;
            addTeamsEmbedBuilder.Description = instructions;
            await channel.SendMessageAsync(embed: addTeamsEmbedBuilder.Build());
        }
    }
}
