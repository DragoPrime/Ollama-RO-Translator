using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.OllamaTranslator
{
    /// <summary>
    /// Setarile configurabile din Dashboard > Plugins > Ollama RO Translator.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// URL-ul serverului Ollama, de exemplu http://192.168.1.10:11434
        /// (foloseste IP-ul containerului/serverului unraid, nu "localhost",
        /// daca Jellyfin si Ollama ruleaza in containere/VM-uri separate).
        /// </summary>
        public string OllamaUrl { get; set; } = "http://127.0.0.1:11434";

        /// <summary>
        /// Numele modelului Ollama folosit pentru traducere.
        /// </summary>
        public string ModelName { get; set; } = "qwen3:8b";

        /// <summary>
        /// Traduce campul Overview (descrierea).
        /// </summary>
        public bool TranslateOverview { get; set; } = true;

        /// <summary>
        /// Traduce genurile (Genres).
        /// </summary>
        public bool TranslateGenres { get; set; } = true;

        /// <summary>
        /// Traduce etichetele (Tags).
        /// </summary>
        public bool TranslateTags { get; set; } = true;

        /// <summary>
        /// Dupa traducere, blocheaza (locked fields) campurile traduse astfel
        /// incat refresh-ul automat de metadate (ex. la 30 de zile) sa nu le
        /// suprascrie cu versiunea in engleza.
        /// </summary>
        public bool LockFieldsAfterTranslation { get; set; } = true;

        /// <summary>
        /// Timeout (in secunde) pentru fiecare cerere catre Ollama. Modelele
        /// locale pot fi lente, mai ales pe hardware modest - creste valoarea
        /// daca vezi erori de timeout in log.
        /// </summary>
        public int RequestTimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Titlurile (Name) NU se traduc niciodata de acest plugin - nu exista
        /// setare pentru asta, in mod intentionat, conform cerintei initiale.
        /// </summary>
        public bool NotesTitlesAreNeverTouched { get; } = true;
    }
}
