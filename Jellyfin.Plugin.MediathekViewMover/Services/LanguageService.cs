using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediathekViewMover.Services
{
    /// <summary>
    /// Service zur Erkennung und Verwaltung von Sprachen.
    /// </summary>
    public class LanguageService
    {
        private readonly ILogger<LanguageService> _logger;
        private CultureInfo[]? _cachedCultures;
        private static readonly char[] Separator = new[] { ' ', '.', '-', '_' };

        /// <summary>
        /// Initializes a new instance of the <see cref="LanguageService"/> class.
        /// </summary>
        /// <param name="logger">Der Logger für den Service.</param>
        public LanguageService(ILogger<LanguageService> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Erkennt die Sprache aus einem Dateinamen oder Text.
        /// </summary>
        /// <param name="name">Der zu analysierende Text.</param>
        /// <param name="secure">Ob kurze Wörter übersprungen werden sollen.</param>
        /// <returns>Die erkannte Kultur oder null.</returns>
        public CultureInfo? GetLanguageFromText(string name, bool secure = true)
        {
            _logger.LogTrace("Suche nach Sprache in: {Name}", name);
            if (_cachedCultures is null || _cachedCultures.Length == 0)
            {
                _cachedCultures = CultureInfo.GetCultures(CultureTypes.AllCultures);
                _logger.LogDebug("Kulturen initialisiert: {Count}", _cachedCultures.Length);
            }

            // Direkte Übereinstimmung prüfen
            var lang = _cachedCultures.FirstOrDefault(culture =>
                culture.ThreeLetterISOLanguageName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                culture.TwoLetterISOLanguageName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                culture.EnglishName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                culture.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                culture.NativeName.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (lang is not null)
            {
                return lang;
            }

            // Suche in extrahierten Sprachstrings
            var languageStrings = ExtractLanguageStrings(name, secure);
            foreach (var word in languageStrings)
            {
                lang = _cachedCultures.FirstOrDefault(culture =>
                    culture.ThreeLetterISOLanguageName.Equals(word, StringComparison.OrdinalIgnoreCase) ||
                    culture.TwoLetterISOLanguageName.Equals(word, StringComparison.OrdinalIgnoreCase) ||
                    culture.EnglishName.Equals(word, StringComparison.OrdinalIgnoreCase) ||
                    culture.Name.Equals(word, StringComparison.OrdinalIgnoreCase) ||
                    culture.NativeName.Equals(word, StringComparison.OrdinalIgnoreCase));

                if (lang is not null)
                {
                    return lang;
                }
            }

            _logger.LogDebug("Keine Sprache gefunden für: {Name}", name);
            return null;
        }

        private List<string> ExtractLanguageStrings(string name, bool secure = true)
        {
            var results = new List<string>();

            // Suche nach Text in Klammern
            string pattern = @"(?<=\()[^)]*(?=\))|(?<=\[)[^\]]*(?=\])";
            var matches = System.Text.RegularExpressions.Regex.Matches(name, pattern);
            foreach (var match in matches.Where(m => m.Success))
            {
                var value = match.Value.Trim();
                if (!results.Contains(value))
                {
                    results.Add(value);
                }
            }

            // Prüfe Dateiendungen
            var ext = name.Split('.');
            for (int i = ext.Length - 1; i >= 0; i--)
            {
                var extName = ext[i].Trim();
                if (extName.Length is 2 or 3)
                {
                    if (!results.Contains(extName))
                    {
                        results.Add(extName);
                    }
                }
                else if (extName.Length > 3)
                {
                    break;
                }
            }

            // Prüfe einzelne Wörter
            foreach (var word in name.Split(Separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (secure && word.Length < 4)
                {
                    continue;
                }

                if (!results.Contains(word))
                {
                    results.Add(word);
                }
            }

            return results;
        }

        /// <summary>
        /// Gibt den Anzeigenamen einer Kultur in der aktuellen UI-Sprache zurück.
        /// </summary>
        /// <param name="culture">Die Kultur.</param>
        /// <returns>Der lokalisierte Anzeigename.</returns>
        public string GetDisplayName(CultureInfo culture)
        {
            return culture.DisplayName;
        }

        /// <summary>
        /// Versucht, die Sprache aus einem Dateinamen zu erkennen.
        /// </summary>
        /// <param name="filePath">Der Dateipfad.</param>
        /// <returns>Der ISO-Sprachcode oder "und" wenn keine Sprache erkannt wurde.</returns>
        public string GetLanguageFromFileName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);
            var culture = GetLanguageFromText(fileName);
            return culture?.ThreeLetterISOLanguageName ?? "und";
        }
    }
}
