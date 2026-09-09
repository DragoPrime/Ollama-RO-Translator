using System;
using System.Collections.Generic;
using System.Linq;

namespace Jellyfin.Plugin.OllamaTranslator
{
    /// <summary>
    /// Detectie euristica, fara apel catre Ollama, folosita pentru a decide daca
    /// un camp este deja in limba romana / deja tradus, ca sa nu fie retradus
    /// inutil (economiseste apeluri catre model si evita suprascrierea unei
    /// traduceri deja bune, facute manual sau de alta unealta).
    /// </summary>
    internal static class LanguageHelper
    {
        private static readonly char[] RomanianDiacritics = { 'ă', 'â', 'î', 'ș', 'ț', 'ş', 'ţ' };

        private static readonly HashSet<string> RomanianStopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "și", "si", "sau", "este", "sunt", "din", "pentru", "care", "după", "asupra",
            "către", "fără", "printre", "despre", "atunci", "acest", "această", "aceste",
            "acestor", "dintre", "până", "dacă", "unde", "cum", "ce", "cu", "pe", "la", "de",
            "în", "un", "o", "al", "ale", "lui", "ei", "el", "ea", "va", "fi", "avea",
            "atât", "mai", "sale", "său", "ori", "iar", "însă", "totuși", "printr-o", "într-un"
        };

        private static readonly HashSet<string> EnglishStopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "and", "of", "in", "is", "was", "with", "that", "for", "from", "this",
            "are", "his", "her", "their", "an", "to", "on", "by", "as", "it", "at", "or",
            "but", "not", "have", "has", "will", "who", "when", "after", "before", "into"
        };

        /// <summary>
        /// Genurile standard in engleza folosite de TMDB / Jellyfin. Daca un item
        /// are cel putin un gen din aceasta lista, consideram ca genurile sunt
        /// inca in engleza si trebuie traduse.
        /// </summary>
        private static readonly HashSet<string> KnownEnglishGenres = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Action", "Action & Adventure", "Adventure", "Animation", "Comedy", "Crime",
            "Documentary", "Drama", "Family", "Fantasy", "History", "Horror", "Kids",
            "Music", "Musical", "Mystery", "News", "Reality", "Romance", "Science Fiction",
            "Sci-Fi & Fantasy", "Soap", "Sport", "Talk", "Thriller", "TV Movie", "War",
            "War & Politics", "Western"
        };

        /// <summary>
        /// Determina, euristic, daca un text este deja in limba romana:
        /// diacritice romanesti sau raport clar in favoarea cuvintelor de legatura
        /// romanesti fata de cele englezesti.
        /// </summary>
        public static bool LooksAlreadyRomanian(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            // Prezenta a cel putin 2 diacritice romanesti este un semnal foarte
            // puternic - practic nu apar in text englezesc normal.
            var diacriticsCount = text.Count(c => RomanianDiacritics.Contains(c));
            if (diacriticsCount >= 2)
            {
                return true;
            }

            var words = text
                .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '"', '(', ')', '-', '\u2013', '\u2014' },
                    StringSplitOptions.RemoveEmptyEntries);

            var roCount = words.Count(RomanianStopwords.Contains);
            var enCount = words.Count(EnglishStopwords.Contains);

            return roCount >= 2 && roCount > enCount;
        }

        /// <summary>
        /// Adevarat daca cel putin un gen din lista este un gen standard in engleza
        /// (deci genurile inca nu au fost traduse).
        /// </summary>
        public static bool HasUntranslatedEnglishGenre(IEnumerable<string>? genres)
        {
            if (genres == null)
            {
                return false;
            }

            return genres.Any(g => KnownEnglishGenres.Contains(g.Trim()));
        }
    }
}
