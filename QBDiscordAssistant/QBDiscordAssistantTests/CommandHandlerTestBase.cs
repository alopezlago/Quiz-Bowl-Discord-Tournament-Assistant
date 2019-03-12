using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QBDiscordAssistant;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

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
            Mock<IUserMessage> mockUserMessage = new Mock<IUserMessage>();

            Mock<IDMChannel> mockDmChannel = new Mock<IDMChannel>();
            mockDmChannel
                .Setup(dmChannel => dmChannel.SendMessageAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<Embed>(), null))
                .Returns<string, bool, Embed, RequestOptions>((message, isTTS, embed, options) =>
                {
                    messageStore.DirectMessages.Add(message);
                    return Task.FromResult(mockUserMessage.Object);
                });

            Mock<IUser> mockUser = new Mock<IUser>();
            mockUser
                .Setup(user => user.GetOrCreateDMChannelAsync(null))
                .Returns(Task.FromResult(mockDmChannel.Object));

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

            // TODO: Need override for this.Context.Guild.CreateTextChannelAsync and VoiceChannelAsync
            List<IGuildChannel> messageChannels = new List<IGuildChannel>();
            mockGuild
                .Setup(guild => guild.CreateTextChannelAsync(It.IsAny<string>(), It.IsAny<Action<TextChannelProperties>>(), null))
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
                            It.IsAny<IRole>(), It.IsAny<OverwritePermissions>(), null))
                        .Returns<IRole, OverwritePermissions, RequestOptions>((role, permissions, options2) =>
                        {
                            textChannelPermissions[role.Id] = permissions;
                            return Task.CompletedTask;
                        });
                    mockTextChannel
                        .Setup(textChannel => textChannel.AddPermissionOverwriteAsync(
                            It.IsAny<IUser>(), It.IsAny<OverwritePermissions>(), null))
                        .Returns<IUser, OverwritePermissions, RequestOptions>((user, permissions, options2) =>
                        {
                            // TODO: Look into adding a value to separate users from roles.
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
                .Setup(guild => guild.GetChannelAsync(It.IsAny<ulong>(), It.IsAny<CacheMode>(), null))
                .Returns<ulong, CacheMode, RequestOptions>((id, cacheMode, options) =>
                {
                    return Task.FromResult(messageChannels[checked((int)id)]);
                });

            if (roleNames != null)
            {
                mockGuild
                    .Setup(guild => guild.Roles)
                    .Returns(() =>
                    {
                        ImmutableList<IRole> roles = ImmutableList<IRole>.Empty;
                        for (int i = 0; i < roleNames.Count; i++)
                        {
                            roles = roles.Add(CreateRole((ulong)i, roleNames[i]));
                        }

                        return roles;
                    });
                mockGuild
                    .Setup(guild => guild.GetRole(It.IsAny<ulong>()))
                    .Returns<ulong>(id =>
                    {
                        int intId = checked((int)id);
                        string name = roleNames[intId];
                        return CreateRole(id, name);
                    });

                mockGuild
                    .Setup(guild => guild.EveryoneRole)
                    .Returns(CreateRole(123456789, "Everyone"));
            }
            

            Mock<IMessageChannel> mockChannel = new Mock<IMessageChannel>();
            mockChannel
                .Setup(channel => channel.SendMessageAsync(It.IsAny<string>(), false, null, null))
                .Returns<string, bool, Embed, RequestOptions>((message, isTTS, embed, options) =>
                {
                    messageStore.ChannelMessages.Add(message);
                    return Task.FromResult(mockUserMessage.Object);
                });
            mockChannel
                .Setup(channel => channel.SendMessageAsync(null, false, It.IsAny<Embed>(), null))
                .Returns<string, bool, Embed, RequestOptions>((message, isTTS, embed, options) =>
                {
                    messageStore.ChannelEmbeds.Add(this.GetMockEmbedText(embed));
                    return Task.FromResult(mockUserMessage.Object);
                });

            Mock<ISelfUser> mockSelfUser = new Mock<ISelfUser>();
            mockSelfUser
                .Setup(selfUser => selfUser.Id)
                .Returns(800);
            Mock<IDiscordClient> mockClient = new Mock<IDiscordClient>();
            mockClient
                .Setup(client => client.CurrentUser)
                .Returns(mockSelfUser.Object);

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

            return mockContext.Object;
        }

        protected IGuildUser CreateGuildUser(ulong userId, List<string> roles = null)
        {
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
                .Returns(() => ImmutableList<ulong>.Empty
                    .AddRange(Enumerable.Range(0, roles.Count)
                    .Select(number => (ulong)number)));
            mockGuildUser
                .Setup(user => user.RemoveRoleAsync(It.IsAny<IRole>(), null))
                .Returns<IRole, RequestOptions>((role, options) =>
                {
                    roles.Remove(role.Name);
                    return Task.CompletedTask;
                });
            mockGuildUser
                .Setup(user => user.AddRoleAsync(It.IsAny<IRole>(), null))
                .Returns<IRole, RequestOptions>((role, options) =>
                {
                    roles.Add(role.Name);
                    return Task.CompletedTask;
                });
            return mockGuildUser.Object;
        }

        protected string GetMockEmbedText(IEmbed embed)
        {
            return GetMockEmbedText(
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
                $"{title}{Environment.NewLine}{description}{Environment.NewLine}{fields}" :
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
