using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using QBDiscordAssistant.Tournament;
using Serilog;
using Game = QBDiscordAssistant.Tournament.Game;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    public class TournamentChannelManager : ITournamentChannelManager
    {
        // Will be needed for tests
        internal static readonly OverwritePermissions PrivilegedOverwritePermissions = new OverwritePermissions(
            speak: PermValue.Allow,
            sendMessages: PermValue.Allow,
            muteMembers: PermValue.Allow,
            deafenMembers: PermValue.Allow,
            readMessageHistory: PermValue.Allow,
            viewChannel: PermValue.Allow,
            moveMembers: PermValue.Allow);
        internal static readonly OverwritePermissions EveryonePermissions = new OverwritePermissions(
            viewChannel: PermValue.Deny,
            sendMessages: PermValue.Deny,
            readMessageHistory: PermValue.Deny);
        internal static readonly OverwritePermissions TeamPermissions = new OverwritePermissions(
            viewChannel: PermValue.Allow,
            sendMessages: PermValue.Allow,
            readMessageHistory: PermValue.Allow);

        public TournamentChannelManager(IGuild guild)
        {
            this.Guild = guild;
            this.Logger = Log
                .ForContext<TournamentChannelManager>()
                .ForContext("guildId", guild?.Id);
        }

        private IGuild Guild { get; }

        private ILogger Logger { get; }

        public async Task CreateChannelsForPrelims(ISelfUser botUser, ITournamentState state, TournamentRoles roles)
        {
            Verify.IsNotNull(this.Guild, "guild");
            Verify.IsNotNull(state, nameof(state));
            Verify.IsNotNull(roles, nameof(roles));

            List<Task<IVoiceChannel>> createVoiceChannelsTasks = new List<Task<IVoiceChannel>>();
            // We only need to go through the games for the first round to get all of the readers.
            Round firstRound = state.Schedule.Rounds.First();
            Debug.Assert(firstRound.Games.Select(game => game.Reader.Name).Count() ==
                firstRound.Games.Select(game => game.Reader.Name).Distinct().Count(),
                "All reader names should be unique.");
            ICategoryChannel voiceCategoryChannel = await this.Guild.CreateCategoryAsync(
                "Readers", options: RequestOptionsSettings.Default);
            foreach (Game game in firstRound.Games)
            {
                createVoiceChannelsTasks.Add(
                    this.CreateVoiceChannelAsync(voiceCategoryChannel, game.Reader));
            }

            IVoiceChannel[] voiceChannels = await Task.WhenAll(createVoiceChannelsTasks);

            // Create the text channels
            const int startingRoundNumber = 1;
            ITextChannel[] textChannels = await this.CreateTextChannelsForRounds(
                botUser, state.Schedule.Rounds, roles, startingRoundNumber);
            IEnumerable<ulong> textCategoryChannelIds = GetCategoryChannelIds(textChannels);

            state.ChannelIds = voiceChannels.Select(channel => channel.Id)
                .Concat(textChannels.Select(channel => channel.Id))
                .Concat(new ulong[] { voiceCategoryChannel.Id })
                .Concat(textCategoryChannelIds)
                .ToArray();
        }

        public async Task CreateChannelsForRebracket(
            ISelfUser botUser,
            ITournamentState state,
            IEnumerable<Round> rounds,
            int startingRoundNumber)
        {
            Verify.IsNotNull(this.Guild, "guild");
            Verify.IsNotNull(state, nameof(state));
            Verify.IsNotNull(rounds, nameof(rounds));

            TournamentRoles roles = this.GetTournamentRoles(state);

            ITextChannel[] textChannels = await this.CreateTextChannelsForRounds(
                botUser, rounds, roles, startingRoundNumber);
            IEnumerable<ulong> textCategoryChannelIds = GetCategoryChannelIds(textChannels);
            state.ChannelIds = state.ChannelIds
                .Concat(textChannels.Select(channel => channel.Id))
                .Concat(textCategoryChannelIds);
        }

        public async Task<ITextChannel> CreateChannelsForFinals(
            ISelfUser botUser, ITournamentState state, Game finalsGame, int finalsRoundNumber, int roomIndex)
        {
            Verify.IsNotNull(this.Guild, "guild");
            Verify.IsNotNull(state, nameof(state));
            Verify.IsNotNull(finalsGame, nameof(finalsGame));

            TournamentRoles roles = this.GetTournamentRoles(state);

            ICategoryChannel finalsCategoryChannel = await this.Guild.CreateCategoryAsync("Finals");
            ITextChannel channel = await this.CreateTextChannelAsync(
                botUser,
                finalsCategoryChannel,
                finalsGame,
                roles,
                finalsRoundNumber,
                roomIndex);

            state.ChannelIds = state.ChannelIds
                .Concat(new ulong[] { channel.Id })
                .Concat(new ulong[] { finalsCategoryChannel.Id });

            return channel;
        }

        private async Task<ITextChannel> CreateTextChannelAsync(
            ISelfUser botUser,
            ICategoryChannel parent,
            Game game,
            TournamentRoles roles,
            int roundNumber,
            int roomNumber)
        {
            Verify.IsNotNull(this.Guild, "guild");
            Verify.IsNotNull(parent, nameof(parent));
            Verify.IsNotNull(game, nameof(game));
            Verify.IsNotNull(roles, nameof(roles));

            // The room and role names will be the same.
            this.Logger.Debug("Creating text channel for room {0} in round {1}", roomNumber, roundNumber);
            string name = GetTextRoomName(game.Reader, roundNumber);
            ITextChannel channel = await this.Guild.CreateTextChannelAsync(
                name,
                channelProps =>
                {
                    channelProps.CategoryId = parent.Id;
                },
                RequestOptionsSettings.Default);
            this.Logger.Debug("Text channel for room {0} in round {1} created", roomNumber, roundNumber);

            // We need to add the bot's permissions first before we disable permissions for everyone.
            await channel.AddPermissionOverwriteAsync(
                botUser, PrivilegedOverwritePermissions, RequestOptionsSettings.Default);

            this.Logger.Debug("Adding permissions to text channel for room {0} in round {1}", roomNumber, roundNumber);
            await channel.AddPermissionOverwriteAsync(
                this.Guild.EveryoneRole, EveryonePermissions, RequestOptionsSettings.Default);
            await channel.AddPermissionOverwriteAsync(
                roles.DirectorRole, PrivilegedOverwritePermissions, RequestOptionsSettings.Default);

            if (roles.RoomReaderRoles.TryGetValue(game.Reader, out IRole readerRole))
            {
                await channel.AddPermissionOverwriteAsync(
                    readerRole, PrivilegedOverwritePermissions, RequestOptionsSettings.Default);
            }
            else
            {
                this.Logger.Warning("Could not find a reader role for a reader with ID {0}.", game.Reader?.Id);
            }

            List<Task> addTeamRolesToChannel = new List<Task>();
            foreach (Team team in game.Teams)
            {
                if (!roles.TeamRoles.TryGetValue(team, out IRole role))
                {
                    this.Logger.Warning("Team {name} did not have a role defined.", team.Name);
                    continue;
                }

                // TODO: Investigate if it's possible to parallelize this. Other attempts to do so (Task.WhenAll,
                // AsyncEnumerable's ParallelForEachAsync) have had bugs where roles sometimes aren't assigned to a
                // channel. Adding an await in the loop seems to be the only thing that 
                await this.AddPermission(channel, role);
            }

            await Task.WhenAll(addTeamRolesToChannel);
            this.Logger.Debug("Added permissions to text channel for room {0} in round {1}", roomNumber, roundNumber);
            return channel;
        }

        private async Task<ITextChannel[]> CreateTextChannelsForRounds(
            ISelfUser botUser,
            IEnumerable<Round> rounds,
            TournamentRoles roles,
            int startingRoundNumber)
        {
            Verify.IsNotNull(this.Guild, "guild");
            Verify.IsNotNull(rounds, nameof(rounds));
            Verify.IsNotNull(roles, nameof(roles));

            List<Task<ITextChannel>> createTextChannelsTasks = new List<Task<ITextChannel>>();
            List<ulong> textCategoryChannelIds = new List<ulong>();
            int roundNumber = startingRoundNumber;
            foreach (Round round in rounds)
            {
                int roomNumber = 0;
                ICategoryChannel roundCategoryChannel = await this.Guild.CreateCategoryAsync(
                    $"Round {roundNumber}",
                    options: RequestOptionsSettings.Default);
                textCategoryChannelIds.Add(roundCategoryChannel.Id);

                foreach (Game game in round.Games)
                {
                    createTextChannelsTasks.Add(this.CreateTextChannelAsync(
                        botUser, roundCategoryChannel, game, roles, roundNumber, roomNumber));
                    roomNumber++;
                }

                roundNumber++;
            }

            ITextChannel[] textChannels = await Task.WhenAll(createTextChannelsTasks);
            return textChannels;
        }

        private async Task<IVoiceChannel> CreateVoiceChannelAsync(ICategoryChannel parent, Reader reader)
        {
            Verify.IsNotNull(this.Guild, "guild");
            Verify.IsNotNull(parent, nameof(parent));
            Verify.IsNotNull(reader, nameof(reader));

            this.Logger.Debug("Creating voice channel for reader {id}", reader.Id);
            string name = GetVoiceRoomName(reader);
            IVoiceChannel channel = await this.Guild.CreateVoiceChannelAsync(
                name,
                channelProps =>
                {
                    channelProps.CategoryId = parent.Id;
                },
                RequestOptionsSettings.Default);
            this.Logger.Debug("Voice channel for reader {id} created", reader.Id);
            return channel;
        }

        private static string GetTextRoomName(Reader reader, int roundNumber)
        {
            return $"Round_{roundNumber}_{reader.Name.Replace(" ", "_", StringComparison.InvariantCulture)}";
        }

        private static string GetVoiceRoomName(Reader reader)
        {
            return $"{reader.Name.Replace(" ", "_", StringComparison.InvariantCulture)}'s_Voice_Channel";
        }

        private static IEnumerable<ulong> GetCategoryChannelIds(IEnumerable<ITextChannel> channels)
        {
            return channels
                .Select(channel => channel.CategoryId)
                .Where(id => id.HasValue)
                .Cast<ulong>()
                .Distinct();
        }

        private async Task AddPermission(IGuildChannel channel, IRole role)
        {
            this.Logger.Debug("Adding role {0} to channel {1}", role.Id, channel.Id);
            await channel.AddPermissionOverwriteAsync(role, TeamPermissions, RequestOptionsSettings.Default);
            this.Logger.Debug("Added role {0} to channel {1}", role.Id, channel.Id);
        }

        private TournamentRoles GetTournamentRoles(IReadOnlyTournamentState state)
        {
            IRole directorRole = this.Guild.GetRole(state.TournamentRoles.DirectorRoleId);
            Dictionary<Reader, IRole> roomReaderRoles = state.TournamentRoles.ReaderRoomRoleIds
                .ToDictionary(kvp => kvp.Key, kvp => this.Guild.GetRole(kvp.Value));
            Dictionary<Team, IRole> teamRoles = state.TournamentRoles.TeamRoleIds
                .ToDictionary(kvp => kvp.Key, kvp => this.Guild.GetRole(kvp.Value));

            return new TournamentRoles()
            {
                DirectorRole = directorRole,
                RoomReaderRoles = roomReaderRoles,
                TeamRoles = teamRoles
            };
        }
    }
}
