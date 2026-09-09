using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.OllamaTranslator
{
    /// <summary>
    /// Task programat (vizibil in Dashboard > Programare sarcini) care parcurge
    /// biblioteca, traduce Overview / Genres / Tags prin Ollama si blocheaza
    /// acele campuri ca sa nu fie suprascrise la refresh-ul automat de metadate.
    ///
    /// Titlurile (Name) si orice alt camp NU sunt atinse niciodata de acest task.
    /// </summary>
    public class OllamaTranslationTask : IScheduledTask, IConfigurableScheduledTask
    {
        /// <summary>
        /// Cheie folosita in ProviderIds pentru a marca un element ca fiind deja
        /// tradus, ca sa nu-l retraducem la fiecare rulare a task-ului.
        /// </summary>
        private const string TranslatedMarkerKey = "OllamaRoTranslated";

        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<OllamaTranslationTask> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        public OllamaTranslationTask(
            ILibraryManager libraryManager,
            ILogger<OllamaTranslationTask> logger,
            IHttpClientFactory httpClientFactory)
        {
            _libraryManager = libraryManager;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public string Name => "Traducere automata RO (Ollama)";

        /// <inheritdoc />
        public string Key => "OllamaRoTranslation";

        /// <inheritdoc />
        public string Description =>
            "Traduce descrierile, genurile si etichetele filmelor/serialelor in limba romana " +
            "folosind Ollama si blocheaza campurile traduse ca sa nu fie suprascrise la refresh.";

        /// <inheritdoc />
        public string Category => "Biblioteca";

        /// <inheritdoc />
        public bool IsHidden => false;

        /// <inheritdoc />
        public bool IsEnabled => true;

        /// <inheritdoc />
        public bool IsLogged => true;

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // Ruleaza implicit zilnic la 04:00 - poti schimba orarul din
            // Dashboard > Programare sarcini. Elementele deja traduse sunt
            // sarite rapid (verificare prin ProviderIds), deci rularile
            // repetate sunt ieftine.
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(4).Ticks
                }
            };
        }

        /// <inheritdoc />
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
        {
            var config = Plugin.Instance!.Configuration;

            if (string.IsNullOrWhiteSpace(config.OllamaUrl) || string.IsNullOrWhiteSpace(config.ModelName))
            {
                _logger.LogWarning(
                    "Ollama RO Translator nu este configurat (OllamaUrl / ModelName lipsesc). " +
                    "Mergi in Dashboard > Plugins > Ollama RO Translator.");
                return;
            }

            var httpClient = _httpClientFactory.CreateClient(nameof(OllamaTranslationTask));
            var client = new OllamaClient(httpClient, config, _logger);

            var query = new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series, BaseItemKind.Episode },
                Recursive = true,
                IsVirtualItem = false
            };

            var items = _libraryManager.GetItemList(query);
            var total = items.Count;
            var processed = 0;
            var translated = 0;

            _logger.LogInformation("Ollama RO Translator: {Total} elemente de verificat.", total);

            foreach (var item in items)
            {
                cancellationToken.ThrowIfCancellationRequested();
                processed++;
                progress.Report(100.0 * processed / Math.Max(total, 1));

                try
                {
                    if (item.ProviderIds.ContainsKey(TranslatedMarkerKey))
                    {
                        // Deja procesat de acest plugin intr-o rulare anterioara.
                        continue;
                    }

                    var existingLockedFields = item.LockedFields ?? Array.Empty<MetadataField>();

                    // Nu atingem un camp care e deja blocat manual (curatat de tine)
                    // sau care pare deja tradus in romana - evitam apeluri inutile
                    // catre Ollama si riscul de a suprascrie o traducere buna.
                    var overviewAlreadyDone =
                        existingLockedFields.Contains(MetadataField.Overview) ||
                        LanguageHelper.LooksAlreadyRomanian(item.Overview);

                    var genresAlreadyDone =
                        existingLockedFields.Contains(MetadataField.Genres) ||
                        (item.Genres != null && item.Genres.Length > 0 && !LanguageHelper.HasUntranslatedEnglishGenre(item.Genres));

                    var tagsAlreadyDone =
                        existingLockedFields.Contains(MetadataField.Tags) ||
                        (item.Tags != null && item.Tags.Length > 0 &&
                         LanguageHelper.LooksAlreadyRomanian(string.Join(" ", item.Tags)));

                    var needsOverview = config.TranslateOverview && !string.IsNullOrWhiteSpace(item.Overview) && !overviewAlreadyDone;
                    var needsGenres = config.TranslateGenres && item.Genres != null && item.Genres.Length > 0 && !genresAlreadyDone;
                    var needsTags = config.TranslateTags && item.Tags != null && item.Tags.Length > 0 && !tagsAlreadyDone;

                    if (!needsOverview && !needsGenres && !needsTags)
                    {
                        // Nimic de tradus - fie e deja in romana, fie e deja blocat manual.
                        // Blocam totusi campurile relevante (daca nu sunt deja) ca sa fie
                        // consecvente cu restul continutului tradus, si marcam itemul ca
                        // "verificat" ca sa nu mai fie reprocesat la fiecare rulare.
                        if (config.LockFieldsAfterTranslation)
                        {
                            var toLock = existingLockedFields.ToList();
                            var changed = false;

                            if (config.TranslateOverview && !string.IsNullOrWhiteSpace(item.Overview) && !toLock.Contains(MetadataField.Overview))
                            {
                                toLock.Add(MetadataField.Overview);
                                changed = true;
                            }

                            if (config.TranslateGenres && item.Genres != null && item.Genres.Length > 0 && !toLock.Contains(MetadataField.Genres))
                            {
                                toLock.Add(MetadataField.Genres);
                                changed = true;
                            }

                            if (config.TranslateTags && item.Tags != null && item.Tags.Length > 0 && !toLock.Contains(MetadataField.Tags))
                            {
                                toLock.Add(MetadataField.Tags);
                                changed = true;
                            }

                            if (changed)
                            {
                                item.LockedFields = toLock.ToArray();
                                await _libraryManager
                                    .UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken)
                                    .ConfigureAwait(false);
                            }
                        }

                        item.ProviderIds[TranslatedMarkerKey] = "1";
                        continue;
                    }

                    var result = await client.TranslateAsync(
                        needsOverview ? item.Overview : null,
                        needsGenres ? item.Genres!.ToList() : new List<string>(),
                        needsTags ? item.Tags!.ToList() : new List<string>(),
                        cancellationToken).ConfigureAwait(false);

                    if (result == null)
                    {
                        // Eroare deja logata in OllamaClient - trecem mai departe,
                        // NU marcam elementul, ca sa fie reincercat data viitoare.
                        continue;
                    }

                    var lockedFields = (item.LockedFields ?? Array.Empty<MetadataField>()).ToList();
                    var anyChange = false;

                    if (needsOverview && !string.IsNullOrWhiteSpace(result.Overview))
                    {
                        item.Overview = result.Overview;
                        anyChange = true;
                        if (config.LockFieldsAfterTranslation && !lockedFields.Contains(MetadataField.Overview))
                        {
                            lockedFields.Add(MetadataField.Overview);
                        }
                    }

                    if (needsGenres && result.Genres.Count > 0)
                    {
                        item.Genres = result.Genres.ToArray();
                        anyChange = true;
                        if (config.LockFieldsAfterTranslation && !lockedFields.Contains(MetadataField.Genres))
                        {
                            lockedFields.Add(MetadataField.Genres);
                        }
                    }

                    if (needsTags && result.Tags.Count > 0)
                    {
                        item.Tags = result.Tags.ToArray();
                        anyChange = true;
                        if (config.LockFieldsAfterTranslation && !lockedFields.Contains(MetadataField.Tags))
                        {
                            lockedFields.Add(MetadataField.Tags);
                        }
                    }

                    if (!anyChange)
                    {
                        continue;
                    }

                    item.LockedFields = lockedFields.ToArray();
                    item.ProviderIds[TranslatedMarkerKey] = "1";

                    await _libraryManager
                        .UpdateItemAsync(item, item.GetParent(), ItemUpdateType.MetadataEdit, cancellationToken)
                        .ConfigureAwait(false);

                    translated++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nu s-a putut traduce elementul '{Name}' ({Id}).", item.Name, item.Id);
                }
            }

            _logger.LogInformation(
                "Ollama RO Translator: task finalizat. {Translated} elemente traduse din {Total} verificate.",
                translated,
                total);
        }
    }
}
