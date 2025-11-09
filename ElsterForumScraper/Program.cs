using HtmlAgilityPack;
using System.Text.RegularExpressions;

namespace ElsterForumScraper
{
    public class Program
    {
        private static readonly HttpClient client = new HttpClient();

        public static async Task Main(string[] args)
        {
            string url = "https://forum.elster.de/anwenderforum/";
            string html = await DownloadHtmlAsync(url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // XPath für die Haupt-Tabelle (anpassen, falls ID/Klasse bekannt; hier generisch)
            var tableRows = doc.DocumentNode.SelectNodes("//table//tr"); // Alle Zeilen in Tabellen finden

            var forums = new List<ForumInfo>();

            if (tableRows != null)
            {
                foreach (var row in tableRows.Skip(1)) // Überspringe Header-Zeile
                {
                    var cells = row.SelectNodes("td");
                    if (cells?.Count >= 4)
                    {
                        var forumNameLink = cells[0].SelectSingleNode(".//a");
                        var forumName = forumNameLink?.InnerText?.Trim() ?? cells[0].InnerText.Trim();
                        var description = ExtractDescription(cells[0]); // Beschreibung nach dem Link
                        var topics = ExtractNumber(cells[1].InnerText, "Themen:");
                        var posts = ExtractNumber(cells[2].InnerText, "Beiträge:");

                        var lastPost = cells[3].SelectSingleNode(".//a");
                        var lastThreadTitle = lastPost?.InnerText?.Trim();
                        var lastAuthor = cells[3].SelectSingleNode(".//a[contains(@href, 'member')]")?.InnerText?.Trim();

                        forums.Add(new ForumInfo
                        {
                            Name = forumName,
                            Description = description,
                            Topics = topics,
                            Posts = posts,
                            LastThread = lastThreadTitle,
                            LastAuthor = lastAuthor
                        });
                    }
                }
            }

            // Ausgabe
            foreach (var forum in forums)
            {
                Console.WriteLine($"Forum: {forum.Name}");
                Console.WriteLine($"  Beschreibung: {forum.Description}");
                Console.WriteLine($"  Themen: {forum.Topics}, Beiträge: {forum.Posts}");
                if (!string.IsNullOrEmpty(forum.LastThread))
                    Console.WriteLine($"  Letzter Thread: '{forum.LastThread}' von {forum.LastAuthor}");
                Console.WriteLine();
            }
        }

        private static async Task<string> DownloadHtmlAsync(string url)
        {
            try
            {
                // User-Agent hinzufügen, um wie ein Browser zu wirken
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                var response = await client.GetStringAsync(url);
                return response;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fehler beim Download: {ex.Message}");
                return string.Empty;
            }
        }

        private static string ExtractDescription(HtmlNode cell)
        {
            // Beschreibung ist oft nach dem ersten <a> in <br> oder <p>
            var textAfterLink = cell.InnerText;
            var linkText = cell.SelectSingleNode(".//a")?.InnerText ?? "";
            return Regex.Replace(textAfterLink.Replace(linkText, "").Trim(), @"\s+", " "); // Saubere Leerzeichen
        }

        private static int ExtractNumber(string text, string prefix)
        {
            var match = Regex.Match(text, $@"{prefix}\s*(\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }

    public class ForumInfo
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public int Topics { get; set; }
        public int Posts { get; set; }
        public string? LastThread { get; set; }
        public string? LastAuthor { get; set; }
    }
}