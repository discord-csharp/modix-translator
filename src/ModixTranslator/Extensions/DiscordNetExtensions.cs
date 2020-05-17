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

        public static string? GetLangFromChannelName(this ITextChannel channel)
        {
            var nameParts = channel.Name.Split('-');
            if (nameParts.Length >= 2)
            {
                return nameParts[1].Replace("_", "-");
            }

            return null;
        }

        public static bool IsStandardLangChannel(this ITextChannel channel)
        {
            return channel.Name.StartsWith("to");
        }
    }
}
