using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;

namespace QBDiscordAssistantTests.Utilities
{
    class MessageStore
    {
        public MessageStore()
        {
            this.DirectMessages = new List<string>();
        }

        public List<string> DirectMessages { get; }

        public void VerifyMessages(params string[] directMessages)
        {
            Assert.AreEqual(directMessages.Length, this.DirectMessages.Count, "Unexpected number of DMs.");
            for (int i = 0; i < directMessages.Length; i++)
            {
                string message = directMessages[i];
                Assert.AreEqual(message, this.DirectMessages[i], $"Unexpected DM at index {i}");
            }
        }
    }
}
