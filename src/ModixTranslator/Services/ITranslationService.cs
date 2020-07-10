using ModixTranslator.Models.TranslationService;
using System.Threading.Tasks;
using ModixTranslator.Models.Translator;

namespace ModixTranslator.Behaviors
{
    public interface ITranslationService
    {
        Task<Translation> GetTranslation(string? from, string to, string text);
        Task<SupportedLanguageResponse> GetSupportedLanguages();
        Task<bool> IsLangSupported(string lang);
    }
}
