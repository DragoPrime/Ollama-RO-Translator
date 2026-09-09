using System;
using System.Collections.Generic;
using System.Globalization;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.OllamaTranslator
{
    /// <summary>
    /// Plugin care traduce automat, prin Ollama, descrierile, genurile si etichetele
    /// continutului din Jellyfin in limba romana si blocheaza acele campuri dupa traducere.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <inheritdoc />
        public override string Name => "Ollama RO Translator";

        /// <summary>
        /// GUID unic al plugin-ului. NU il schimba dupa prima lansare publica -
        /// Jellyfin foloseste acest ID pentru a identifica plugin-ul instalat.
        /// </summary>
        public override Guid Id => Guid.Parse("8f3c1a2e-4b7d-4e5f-9c1a-2e4b7d4e5f9c");

        /// <inheritdoc />
        public override string Description =>
            "Traduce automat descrierile, genurile si etichetele in limba romana " +
            "folosind un server Ollama local, apoi blocheaza campurile traduse.";

        public static Plugin? Instance { get; private set; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "OllamaTranslator",
                    EmbeddedResourcePath = string.Format(
                        CultureInfo.InvariantCulture,
                        "{0}.Configuration.configPage.html",
                        GetType().Namespace)
                }
            };
        }
    }
}
