using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QBDiscordAssistant;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;

namespace QBDiscordAssistantTests
{
    public class CommandHandlerTestBase
    {
        protected const ulong DefaultGuildId = 1;
        protected const string DefaultGuildName = "Test Guild";
        protected const string DefaultTournamentName = "My Tournament";

        protected ITournamentState AddCurrentTournament(
            GlobalTournamentsManager globalManager,
            ulong guildId = DefaultGuildId,
            string tournamentName = DefaultTournamentName)
        {
            TournamentsManager manager = globalManager.GetOrAdd(guildId, id => new TournamentsManager());
            ITournamentState state = new TournamentState(guildId, tournamentName);
            state = manager.AddOrUpdateTournament(tournamentName, state, (name, oldState) => state);
            Assert.IsTrue(
                manager.TrySetCurrentTournament(tournamentName, out string errorMessage),
                "We should be able to set the current tournament.");
            return state;
        }

        protected ICommandContext CreateCommandContext(
            MessageStore messageStore,
            ulong guildId = DefaultGuildId,
            string guildName = DefaultGuildName,
            List<string> roleNames = null)
        {
            return this.CreateMockCommandContext(messageStore, guildId, guildName, roleNames).Object;
        }

        protected Mock<IDiscordClient> CreateMockClient()
        {
            Mock<ISelfUser> mockSelfUser = new Mock<ISelfUser>();
            mockSelfUser
                .Setup(selfUser => selfUser.Id)
                .Returns(800);
            Mock<IDiscordClient> mockClient = new Mock<IDiscordClient>();
            mockClient
                .Setup(client => client.CurrentUser)
                .Returns(mockSelfUser.Object);
            return mockClient;
        }

        protected Mock<ICommandContext> CreateMockCommandContext(
            MessageStore messageStore,
            ulong guildId = DefaultGuildId,
            string guildName = DefaultGuildName,
            List<string> roleNames = null)
        {
            Mock<IGuild> mockGuild = this.CreateMockGuild(guildId, guildName, roleNames);
            Mock<IMessageChannel> mockChannel = this.CreateMockMessageChannel(messageStore);
            Mock<IDiscordClient> mockClient = this.CreateMockClient();
            Mock<IUser> mockUser = this.CreateMockUser(messageStore);
            return this.CreateMockCommandContext(mockGuild, mockChannel, mockClient, mockUser);
        }

        protected Mock<ICommandContext> CreateMockCommandContext(
            Mock<IGuild> mockGuild,
            Mock<IMessageChannel> mockChannel,
            Mock<IDiscordClient> mockClient,
            Mock<IUser> mockUser)
        {
            Mock<ICommandContext> mockContext = new Mock<ICommandContext>();
            mockContext
                .Setup(context => context.Guild)
                .Returns(mockGuild.Object);
            mockContext
                .Setup(context => context.User)
                .Returns(mockUser.Object);
            mockContext
                .Setup(context => context.Channel)
                .Returns(mockChannel.Object);
            mockContext
                .Setup(context => context.Client)
                .Returns(mockClient.Object);

            return mockContext;
        }

        protected Mock<IGuild> CreateMockGuild(
            ulong guildId = DefaultGuildId,
            string guildName = DefaultGuildName,
            List<string> roleNames = null)
        {
            Mock<IGuild> mockGuild = new Mock<IGuild>();
            mockGuild
                .Setup(guild => guild.Id)
                .Returns(guildId);
            mockGuild
                .Setup(guild => guild.Name)
                .Returns(guildName);
            mockGuild
                .Setup(guild => guild.GetUserAsync(It.IsAny<ulong>(), It.IsAny<CacheMode>(), It.IsAny<RequestOptions>()))
                .Returns<ulong, CacheMode, RequestOptions>(
                    (id, cacheMode, requestOptions) => Task.FromResult(this.CreateGuildUser(id)));

            List<IGuildChannel> messageChannels = new List<IGuildChannel>();
            mockGuild
                .Setup(guild => guild.CreateTextChannelAsync(It.IsAny<string>(), It.IsAny<Action<TextChannelProperties>>(), It.IsAny<RequestOptions>()))
                .Returns<string, Action<TextChannelProperties>, RequestOptions>((name, func, options) =>
                {
                    Dictionary<ulong, OverwritePermissions> textChannelPermissions = new Dictionary<ulong, OverwritePermissions>();
                    Mock<ITextChannel> mockTextChannel = new Mock<ITextChannel>();
                    mockTextChannel
                        .Setup(textChannel => textChannel.Name)
                        .Returns(name);
                    mockTextChannel
                        .Setup(textChannel => textChannel.Mention)
                        .Returns($"@{name}");
                    mockTextChannel
                        .Setup(textChannel => textChannel.Id)
                        .Returns((ulong)messageChannels.Count);
                    mockTextChannel
                        .Setup(textChannel => textChannel.AddPermissionOverwriteAsync(
                            It.IsAny<IRole>(), It.IsAny<OverwritePermissions>(), It.IsAny<RequestOptions>()))
                        .Returns<IRole, OverwritePermissions, RequestOptions>((role, permissions, options2) =>
                        {
                            textChannelPermissions[role.Id] = permissions;
                            return Task.CompletedTask;
                        });
                    mockTextChannel
                        .Setup(textChannel => textChannel.AddPermissionOverwriteAsync(
                            It.IsAny<IUser>(), It.IsAny<OverwritePermissions>(), It.IsAny<RequestOptions>()))
                        .Returns<IUser, OverwritePermissions, RequestOptions>((user, permissions, options2) =>
                        {
                            textChannelPermissions[user.Id] = permissions;
                            return Task.CompletedTask;
                        });
                    mockTextChannel
                        .Setup(textChannel => textChannel.GetPermissionOverwrite(It.IsAny<IRole>()))
                        .Returns<IRole>((role) =>
                        {
                            if (textChannelPermissions.TryGetValue(
                                role.Id, out OverwritePermissions overwritePermissions))
                            {
                                return overwritePermissions;
                            }

                            return null;
                        });
                    mockTextChannel
                        .Setup(textChannel => textChannel.GetPermissionOverwrite(It.IsAny<IUser>()))
                        .Returns<IUser>((user) =>
                        {
                            if (textChannelPermissions.TryGetValue(
                                user.Id, out OverwritePermissions overwritePermissions))
                            {
                                return overwritePermissions;
                            }

                            return null;
                        });

                    // TODO: Add mock for category ID. Issue is that we need TextChannelProperties to pass in.
                    messageChannels.Add(mockTextChannel.Object);
                    return Task.FromResult(mockTextChannel.Object);
                });

            mockGuild
                .Setup(guild => guild.GetChannelAsync(It.IsAny<ulong>(), It.IsAny<CacheMode>(), It.IsAny<RequestOptions>()))
                .Returns<ulong, CacheMode, RequestOptions>((id, cacheMode, options) =>
                {
                    return Task.FromResult(messageChannels[checked((int)id)]);
                });
            mockGuild
                .Setup(guild => guild.GetChannelsAsync(It.IsAny<CacheMode>(), It.IsAny<RequestOptions>()))
                .Returns<CacheMode, RequestOptions>((cacheMode, options) =>
                {
                    IReadOnlyCollection<IGuildChannel> channels = ImmutableArray.Create(messageChannels.ToArray());
                    return Task.FromResult(channels);
                });
            mockGuild
                .Setup(guild => guild.CreateCategoryAsync(It.IsAny<string>(), null, It.IsAny<RequestOptions>()))
                .Returns<string, Action<GuildChannelProperties>, RequestOptions>((name, func, options) =>
                {
                    Mock<ICategoryChannel> mockCategoryChannel = new Mock<ICategoryChannel>();
                    mockCategoryChannel
                        .Setup(channel => channel.Id)
                        .Returns((ulong)messageChannels.Count);
                    mockCategoryChannel
                        .Setup(channel => channel.Name)
                        .Returns(name);
                    messageChannels.Add(mockCategoryChannel.Object);
                    return Task.FromResult(mockCategoryChannel.Object);
                });
            mockGuild
                .Setup(guild => guild.CreateVoiceChannelAsync(
                    It.IsAny<string>(), It.IsAny<Action<VoiceChannelProperties>>(), It.IsAny<RequestOptions>()))
                .Returns<string, Action<VoiceChannelProperties>, RequestOptions>((name, func, options) =>
                {
                    // TODO: Figure out how to run the func.
                    Mock<IVoiceChannel> mockVoiceChannel = new Mock<IVoiceChannel>();
                    mockVoiceChannel
                        .Setup(channel => channel.Id)
                        .Returns((ulong)messageChannels.Count);
                    mockVoiceChannel
                        .Setup(channel => channel.Name)
                        .Returns(name);
                    messageChannels.Add(mockVoiceChannel.Object);
                    return Task.FromResult(mockVoiceChannel.Object);
                });

            if (roleNames != null)
            {
                ImmutableList<IRole> roles = ImmutableList<IRole>.Empty;
                for (int i = 0; i < roleNames.Count; i++)
                {
                    roles = roles.Add(CreateRole((ulong)(i + 1), roleNames[i]));
                }

                mockGuild
                    .Setup(guild => guild.Roles)
                    .Returns(() => roles);
                mockGuild
                    .Setup(guild => guild.GetRole(It.IsAny<ulong>()))
                    .Returns<ulong>(id =>
                    {
                        int intId = checked((int)(id - 1));
                        string name = roleNames[intId];
                        return CreateRole(id, name);
                    });
                mockGuild
                    .Setup(guild => guild.CreateRoleAsync(
                        It.IsAny<string>(),
                        It.IsAny<GuildPermissions?>(),
                        It.IsAny<Color?>(),
                        It.IsAny<bool>(),
                        It.IsAny<RequestOptions>()))
                    .Returns<string, GuildPermissions?, Color?, bool, RequestOptions>(
                        (name, permissions, color, isHoisted, options) =>
                        {
                            IRole role = CreateRole((ulong)roles.Count, name);
                            roles = roles.Add(role);
                            return Task.FromResult(role);
                        });

                mockGuild
                    .Setup(guild => guild.EveryoneRole)
                    .Returns(CreateRole(123456789, "Everyone"));
            }

            return mockGuild;
        }

        protected IGuildUser CreateGuildUser(ulong userId, List<string> roles = null)
        {
            if (roles == null)
            {
                roles = new List<string>();
            }

            List<ulong> roleIds = new List<ulong>(Enumerable.Range(1, roles.Count).Select(number => (ulong)number));

            Mock<IGuildUser> mockGuildUser = new Mock<IGuildUser>();
            mockGuildUser
                .Setup(user => user.Id)
                .Returns(userId);
            mockGuildUser
                .Setup(user => user.Nickname)
                .Returns($"Nickname{userId}");
            mockGuildUser
                .Setup(user => user.Mention)
                .Returns($"@User{userId}");
            mockGuildUser
                .Setup(user => user.RoleIds)
                .Returns(() => roleIds);
            mockGuildUser
                .Setup(user => user.RemoveRoleAsync(It.IsAny<IRole>(), It.IsAny<RequestOptions>()))
                .Returns<IRole, RequestOptions>((role, options) =>
                {
                    roles.Remove(role.Name);
                    roleIds.Remove(role.Id);
                    return Task.CompletedTask;
                });
            mockGuildUser
                .Setup(user => user.RemoveRolesAsync(It.IsAny<IEnumerable<IRole>>(), It.IsAny<RequestOptions>()))
                .Returns<IEnumerable<IRole>, RequestOptions>((removedRoles, options) =>
                {
                    // removedRoles can come from roles or roleIds, so make a copy of it and enumerate through that
                    IRole[] removedRolesArray = removedRoles.ToArray();
                    foreach (IRole role in removedRolesArray)
                    {
                        roles.Remove(role.Name);
                        roleIds.Remove(role.Id);
                    }

                    return Task.CompletedTask;
                });
            mockGuildUser
                .Setup(user => user.AddRoleAsync(It.IsAny<IRole>(), It.IsAny<RequestOptions>()))
                .Returns<IRole, RequestOptions>((role, options) =>
                {
                    roles.Add(role.Name);
                    roleIds.Add(role.Id);
                    return Task.CompletedTask;
                });
            return mockGuildUser.Object;
        }

        protected Mock<IMessageChannel> CreateMockMessageChannel(MessageStore messageStore)
        {
            Mock<IUserMessage> mockUserMessage = new Mock<IUserMessage>();

            Mock<IMessageChannel> mockChannel = new Mock<IMessageChannel>();
            mockChannel
                .Setup(channel => channel.SendMessageAsync(It.IsAny<string>(), false, null, It.IsAny<RequestOptions>()))
                .Returns<string, bool, Embed, RequestOptions>((message, isTTS, embed, options) =>
                {
                    messageStore.ChannelMessages.Add(message);
                    return Task.FromResult(mockUserMessage.Object);
                });
            mockChannel
                .Setup(channel => channel.SendMessageAsync(null, false, It.IsAny<Embed>(), It.IsAny<RequestOptions>()))
                .Returns<string, bool, Embed, RequestOptions>((message, isTTS, embed, options) =>
                {
                    messageStore.ChannelEmbeds.Add(this.GetMockEmbedText(embed));
                    return Task.FromResult(mockUserMessage.Object);
                });
            return mockChannel;
        }

        protected Mock<IUser> CreateMockUser(MessageStore messageStore)
        {
            Mock<IUserMessage> mockUserMessage = new Mock<IUserMessage>();
            Mock<IDMChannel> mockDmChannel = new Mock<IDMChannel>();
            mockDmChannel
                .Setup(dmChannel => dmChannel.SendMessageAsync(
                    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), It.IsAny<RequestOptions>()))
                .Returns<string, bool, Embed, RequestOptions>((message, isTTS, embed, options) =>
                {
                    messageStore.DirectMessages.Add(message);
                    return Task.FromResult(mockUserMessage.Object);
                });

            Mock<IUser> mockUser = new Mock<IUser>();
            mockUser
                .Setup(user => user.GetOrCreateDMChannelAsync(null))
                .Returns(Task.FromResult(mockDmChannel.Object));
            return mockUser;
        }

        protected string GetMockEmbedText(IEmbed embed)
        {
            if (embed == null)
            {
                throw new ArgumentNullException(nameof(embed));
            }

            return this.GetMockEmbedText(
                embed.Title, embed.Description, embed.Fields.ToDictionary(field => field.Name, field => field.Value));
        }

        protected string GetMockEmbedText(string title, string description, IDictionary<string, string> fields = null)
        {
            string fieldsText = string.Empty;
            if (fields != null)
            {
                fieldsText = string.Join(
                    Environment.NewLine, fields.Select(field => $"{field.Key}: {field.Value}"));
            }
            string embedText = fieldsText.Length > 0 ?
                $"{title}{Environment.NewLine}{description}{Environment.NewLine}{fieldsText}" :
                $"{title}{Environment.NewLine}{description}";
            return embedText;
        }

        private static IRole CreateRole(ulong id, string name)
        {
            Mock<IRole> mockRole = new Mock<IRole>();
            mockRole
                .Setup(role => role.Id)
                .Returns(id);
            mockRole
                .Setup(role => role.Name)
                .Returns(name);
            return mockRole.Object;
        }
    }
}
