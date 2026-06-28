using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace MenuApp
{
    static class UpdateChecker
    {
        public const string CurrentVersion = "1.1.2";

        private const string ApiUrl       = "https://api.github.com/repos/andrey1b/MenuApp/releases/latest";
        public  const string ReleasesUrl  = "https://github.com/andrey1b/MenuApp/releases/latest";

        // Проверяет последнюю версию на GitHub.
        // onResult(null)        — актуальна или нет сети;
        // onResult("1.0.5")     — доступна новая версия (без префикса v).
        // Колбэк вызывается в фоновом потоке — UI трогать через BeginInvoke.
        public static async Task CheckAsync(Action<string?> onResult)
        {
            string? newer = null;
            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "MenuApp/" + CurrentVersion);
                http.Timeout = TimeSpan.FromSeconds(10);

                var json = await http.GetStringAsync(ApiUrl);
                using var doc = JsonDocument.Parse(json);
                var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
                var latestVersion = tag.TrimStart('v');

                if (IsNewer(latestVersion, CurrentVersion))
                    newer = latestVersion;
            }
            catch { newer = null; }

            onResult(newer);
        }

        private static bool IsNewer(string latest, string current)
        {
            return Version.TryParse(latest, out var vLatest)
                && Version.TryParse(current, out var vCurrent)
                && vLatest > vCurrent;
        }
    }
}
