using ModixTranslator.Models.TranslationService;
using System.Threading.Tasks;

namespace ModixTranslator.Behaviors
{
    public interface ITranslationService
    {
        Task<string> GetTranslation(string? from, string to, string text);
        Task<SupportedLanguageResponse> GetSupportedLanguages();
        Task<bool> IsLangSupported(string lang);
    }
}
