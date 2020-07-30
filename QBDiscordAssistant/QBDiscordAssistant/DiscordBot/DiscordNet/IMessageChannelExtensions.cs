using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;

namespace QBDiscordAssistant.DiscordBot.DiscordNet
{
    internal static class IMessageChannelExtensions
    {
        private const int MaxFieldsInEmbed = 20;
        private const int MaxEmbedLength = 6000;

        // We enumerate through each field to create an list of EmbedFieldBuidlers. Then we can do both
        // the item limits and item length limits while iterating through that list, since we can access the index.
        // I tried just doing it inline when going through the collection, but the logic for handling edge cases is
        // messier.

        public static async Task SendAllEmbeds<T>(
            this IMessageChannel channel,
            IEnumerable<T> collection,
            Func<EmbedBuilder> createEmbedBuilder,
            Func<T, int, EmbedFieldBuilder> createField,
            Action<IUserMessage, Embed> postMessageAction = null)
        {
            Verify.IsNotNull(channel, nameof(channel));
            Verify.IsNotNull(collection, nameof(collection));
            Verify.IsNotNull(createEmbedBuilder, nameof(createEmbedBuilder));
            Verify.IsNotNull(createField, nameof(createField));

            List<EmbedFieldBuilder> fields = new List<EmbedFieldBuilder>();
            int index = 0;
            foreach (T item in collection)
            {
                fields.Add(createField(item, index));
                index++;
            }

            int fieldIndex = 0;
            while (fieldIndex < fields.Count)
            {
                EmbedBuilder embedBuilder = createEmbedBuilder();
                int embedLength = 0;

                while (embedBuilder.Fields.Count < MaxFieldsInEmbed && fieldIndex < fields.Count)
                {
                    EmbedFieldBuilder field = fields[fieldIndex];
                    int fieldLength = GetEmbedFieldLength(field);

                    if (fieldLength > MaxEmbedLength)
                    {
                        // We will never be able to add this embed. Fail.
                        throw new ArgumentException(
                            $"Collection contains a field that is too large. Index: {fieldIndex}", nameof(collection));
                    }

                    embedLength += fieldLength;
                    if (embedLength >= MaxEmbedLength)
                    {
                        // This field would push us over the limit, so sotp for now.
                        break;
                    }

                    fieldIndex++;
                    embedBuilder.AddField(field);
                }

                Embed embed = embedBuilder.Build();
                IUserMessage newMessage = await channel.SendMessageAsync(
                    embed: embed, options: RequestOptionsSettings.Default);

                postMessageAction?.Invoke(newMessage, embed);
            }
        }

        private static int GetEmbedFieldLength(EmbedFieldBuilder fieldBuilder)
        {
            return checked((fieldBuilder.Name?.Length ?? 0) + (fieldBuilder.Value?.ToString().Length ?? 0));
        }
    }
}
