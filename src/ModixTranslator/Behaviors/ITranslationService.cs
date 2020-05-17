using System.Threading.Tasks;

namespace ModixTranslator.Behaviors
{
    public interface ITranslationService
    {
        Task<string> GetTranslation(string from, string to, string text);
        Task<bool> IsLangSupported(string lang);
    }
}
