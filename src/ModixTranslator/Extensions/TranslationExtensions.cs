using System;
using System.Collections.Generic;
using Discord;

namespace ModixTranslator.Extensions
{
    public static class TranslationExtensions
    {
        public static EmbedBuilder AddChunks(
            this EmbedBuilder builder,
            IEnumerable<string> chunks, string lang,
            bool inline = false)
        {
            foreach (var chunk in chunks)
            {
                builder.AddField(lang, chunk, inline);
            }

            return builder;
        }

        public static IEnumerable<string> ChunkUpTo(this string str, int maxChunkSize)
        {
            for (var i = 0; i < str.Length; i += maxChunkSize)
            {
                yield return str.Substring(i, Math.Min(maxChunkSize, str.Length - i));
            }
        }
    }
}