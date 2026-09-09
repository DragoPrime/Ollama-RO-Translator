using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OllamaTranslator
{
    /// <summary>
    /// Rezultatul traducerii intors de model.
    /// </summary>
    public class TranslationResult
    {
        [JsonPropertyName("overview")]
        public string Overview { get; set; } = string.Empty;

        [JsonPropertyName("genres")]
        public List<string> Genres { get; set; } = new List<string>();

        [JsonPropertyName("tags")]
        public List<string> Tags { get; set; } = new List<string>();
    }

    internal class OllamaGenerateRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } = string.Empty;

        [JsonPropertyName("prompt")]
        public string Prompt { get; set; } = string.Empty;

        [JsonPropertyName("format")]
        public string Format { get; set; } = "json";

        [JsonPropertyName("stream")]
        public bool Stream { get; set; }

        [JsonPropertyName("options")]
        public OllamaOptions Options { get; set; } = new OllamaOptions();
    }

    internal class OllamaOptions
    {
        [JsonPropertyName("temperature")]
        public double Temperature { get; set; } = 0.2;
    }

    internal class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; set; } = string.Empty;
    }

    /// <summary>
    /// Client minimal pentru API-ul /api/generate al Ollama, folosit pentru
    /// a cere traducerea structurata (JSON) a metadatelor.
    /// </summary>
    public class OllamaClient
    {
        private readonly HttpClient _httpClient;
        private readonly PluginConfiguration _config;
        private readonly ILogger _logger;

        public OllamaClient(HttpClient httpClient, PluginConfiguration config, ILogger logger)
        {
            _httpClient = httpClient;
            _config = config;
            _logger = logger;
            _httpClient.Timeout = TimeSpan.FromSeconds(Math.Max(config.RequestTimeoutSeconds, 10));
        }

        /// <summary>
        /// Trimite continutul de tradus catre Ollama si intoarce rezultatul structurat,
        /// sau null daca cererea/parsarea a esuat (eroarea e logata, nu arunca exceptie
        /// in sus catre task pentru a nu opri procesarea intregii biblioteci).
        /// </summary>
        public async Task<TranslationResult?> TranslateAsync(
            string? overview,
            List<string> genres,
            List<string> tags,
            CancellationToken cancellationToken)
        {
            var payload = new
            {
                overview = overview ?? string.Empty,
                genres,
                tags
            };

            var payloadJson = JsonSerializer.Serialize(payload);

            var prompt =
                "Esti un traducator profesionist de metadate pentru filme si seriale.\n" +
                "Tradu in limba romana CONTINUTUL din JSON-ul de mai jos (campurile \"overview\", \"genres\", \"tags\").\n" +
                "Reguli stricte:\n" +
                "- Raspunde DOAR cu un obiect JSON valid, fara explicatii, fara text in plus, fara markdown.\n" +
                "- Structura raspunsului trebuie sa fie exact: {\"overview\": string, \"genres\": [string], \"tags\": [string]}.\n" +
                "- Pastreaza acelasi numar de elemente in \"genres\" si in \"tags\" ca in JSON-ul de intrare (traducere 1 la 1, in aceeasi ordine).\n" +
                "- Genurile trebuie traduse natural, asa cum sunt folosite uzual pe platformele de streaming in limba romana " +
                "(ex: \"Action\" -> \"Actiune\", \"Science Fiction\" -> \"SF\", \"Comedy\" -> \"Comedie\").\n" +
                "- Nu traduce nume proprii, titluri de filme/seriale sau nume de persoane care apar in text.\n" +
                "- Daca un camp este gol in JSON-ul de intrare, returneaza-l gol (string gol sau lista goala).\n\n" +
                "JSON de intrare:\n" + payloadJson;

            var request = new OllamaGenerateRequest
            {
                Model = _config.ModelName,
                Prompt = prompt
            };

            var url = _config.OllamaUrl.TrimEnd('/') + "/api/generate";

            try
            {
                using var response = await _httpClient
                    .PostAsJsonAsync(url, request, cancellationToken)
                    .ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                var body = await response.Content
                    .ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                if (body == null || string.IsNullOrWhiteSpace(body.Response))
                {
                    _logger.LogWarning("Raspuns gol de la Ollama la {Url}", url);
                    return null;
                }

                return JsonSerializer.Deserialize<TranslationResult>(
                    body.Response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Nu m-am putut conecta la Ollama la {Url}. Verifica OllamaUrl din configurare.", url);
                return null;
            }
            catch (TaskCanceledException ex)
            {
                _logger.LogWarning(ex, "Cererea catre Ollama a depasit timeout-ul configurat.");
                return null;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Raspunsul de la Ollama nu a putut fi interpretat ca JSON valid.");
                return null;
            }
        }
    }
}
