
namespace ModixTranslator
{
    public class TranslationConstants
    {
        public const string CategoryName = "localized";
        public const string HowToChannelName = "how-to";
        public const string HistoryChannelName = "history";
        public static readonly string[] PermanentChannels = new[] { HowToChannelName, HistoryChannelName };
        public const string StandardLanguage = "en";
        public const int IdleChannelTimeoutMinutes = 240;
    }
}
