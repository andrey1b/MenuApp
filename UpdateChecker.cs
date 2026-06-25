using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MenuApp
{
    static class UpdateChecker
    {
        public const string CurrentVersion = "1.0.2";

        private const string ApiUrl      = "https://api.github.com/repos/andrey1b/MenuApp/releases/latest";
        private const string ReleasesUrl = "https://github.com/andrey1b/MenuApp/releases/latest";

        public static async Task CheckAsync()
        {
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
                {
                    var result = MessageBox.Show(
                        $"Доступна новая версия {latestVersion}.\nОткрыть страницу загрузки?",
                        "Обновление MenuApp",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information);

                    if (result == DialogResult.Yes)
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = ReleasesUrl,
                            UseShellExecute = true
                        });
                }
            }
            catch { }
        }

        private static bool IsNewer(string latest, string current)
        {
            return Version.TryParse(latest, out var vLatest)
                && Version.TryParse(current, out var vCurrent)
                && vLatest > vCurrent;
        }
    }
}
