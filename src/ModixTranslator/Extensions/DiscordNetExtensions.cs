using Discord;
using Microsoft.Extensions.Logging;

namespace ModixTranslator
{
    public static class DiscordNetExtensions
    {
        public static LogLevel ToLogLevel(this LogSeverity severity)
        {
            return severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Debug,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Debug
            };
        }

        public static string? GetLangFromChannelName(this ITextChannel channel, string serverLanguage)
        {
            var nameParts = channel.Name.Split('-');
            if (nameParts.Length != 3)
            {
                return null;
            }

            string? loc;
            if (nameParts[0] == serverLanguage)
            {
                loc = nameParts[2];
            }
            else
            {
                loc = nameParts[0];
            }

            return loc?.Replace("_", "-");
        }

        public static bool IsStandardLangChannel(this ITextChannel channel, string serverLanguage)
        {
            return channel.Name.StartsWith(serverLanguage);
        }
    }
}
