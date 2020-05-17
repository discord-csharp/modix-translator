
namespace TranslatorBot9000
{
    public class LocalizationConstants
    {
        public const string CategoryName = "localized";
        public const string HowToChannelName = "how-to";
        public const string HistoryChannelName = "history";
        public static readonly string[] PermanentChannels = new[] { HowToChannelName, HistoryChannelName };
        public const string TempChannelNameTemplate = "from-{from}-to-{to}";
    }
}
