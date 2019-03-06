using Discord;
using Discord.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using QBDiscordAssistant;
using QBDiscordAssistant.Tournament;
using QBDiscordAssistantTests.Utilities;
using System;
using System.Collections.Generic;
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
            MessageStore messageStore, ulong guildId = DefaultGuildId, string guildName = DefaultGuildName)
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

            Mock<ICommandContext> mockContext = new Mock<ICommandContext>();

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

            mockContext
                .Setup(context => context.Guild)
                .Returns(mockGuild.Object);
            mockContext
                .Setup(context => context.User)
                .Returns(mockUser.Object);
            mockContext
                .Setup(context => context.Channel)
                .Returns(mockChannel.Object);

            return mockContext.Object;
        }

        protected IGuildUser CreateGuildUser(ulong userId)
        {
            Mock<IGuildUser> mockGuildUser = new Mock<IGuildUser>();
            mockGuildUser
                .Setup(user => user.Id)
                .Returns(userId);
            mockGuildUser
                .Setup(user => user.Nickname)
                .Returns($"Nickname{userId}");
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
    }
}
