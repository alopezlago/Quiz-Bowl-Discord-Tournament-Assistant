using Discord;
using Discord.Commands;
using Moq;
using System.Threading.Tasks;

namespace QBDiscordAssistantTests.Utilities
{
    static class MockICommandContextFactory
    {
        public static ICommandContext CreateCommandContext(ulong guildId, string guildName, MessageStore messageStore)
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
            mockContext
                .Setup(context => context.Guild)
                .Returns(mockGuild.Object);
            mockContext
                .Setup(context => context.User)
                .Returns(mockUser.Object);

            return mockContext.Object;
        }
    }
}
