using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Text.RegularExpressions;
using System.Text.Json; // Für optionale JSON-Ausgabe
using System.IO; // Für File.WriteAllTextAsync

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
            
            // XPath: Fokussiert auf Forum-Tabelle (class 'forumline' oder width=100%)
            var tableRows = doc.DocumentNode.SelectNodes("//table[contains(@class, 'forum') or @width='100%']//tr | //table//tr");
            
            var forums = new List<ForumInfo>();
            
            if (tableRows != null)
            {
                foreach (var row in tableRows)
                {
                    var cells = row.SelectNodes("td");
                    if (cells?.Count == 4 && cells[0].SelectSingleNode(".//a") != null) // Nur Subforums
                    {
                        var forumNameLink = cells[0].SelectSingleNode(".//a");
                        var forumName = forumNameLink?.InnerText?.Trim() ?? cells[0].InnerText.Trim();
                        
                        var description = ExtractDescription(cells[0]);
                        
                        // Topics/Posts: Direkte Parse (deutsche Punkte → int → :N0)
                        var topicsStr = cells[1]?.InnerText?.Trim().Replace(".", "") ?? "0";
                        var postsStr = cells[2]?.InnerText?.Trim().Replace(".", "") ?? "0";
                        var topics = int.TryParse(topicsStr, out int t) ? t : 0;
                        var posts = int.TryParse(postsStr, out int p) ? p : 0;
                        
                        // Last Post: Title aus TD1 (falls "Letzter Beitrag:"), Author aus TD4
                        var lastThreadTitle = ExtractLastThreadTitle(cells[0]);
                        var lastAuthor = ExtractLastAuthor(cells[3]);
                        
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
            
            // Konsole-Ausgabe
            foreach (var forum in forums)
            {
                Console.WriteLine($"Forum: {forum.Name}");
                Console.WriteLine($"  Beschreibung: {forum.Description}");
                Console.WriteLine($"  Themen: {forum.Topics:N0}, Beiträge: {forum.Posts:N0}");
                if (!string.IsNullOrEmpty(forum.LastThread))
                    Console.WriteLine($"  Letzter Thread: '{forum.LastThread}' von {forum.LastAuthor ?? "Unbekannt"}");
                Console.WriteLine();
            }
            
            // JSON-Export aktivieren (uncomment für Datei)
            var json = JsonSerializer.Serialize(forums, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync("forums.json", json);
            Console.WriteLine("Daten als JSON in forums.json gespeichert. (Für Analysen/CSV-Export super!)");
        }
        
        private static async Task<string> DownloadHtmlAsync(string url)
        {
            try
            {
                client.DefaultRequestHeaders.Clear();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
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
            var fullText = cell.InnerText;
            var linkText = cell.SelectSingleNode(".//a")?.InnerText ?? "";
            
            var textAfterLink = fullText.Replace(linkText, "").Trim();
            textAfterLink = Regex.Replace(textAfterLink, @"\s+", " "); // Clean spaces &nbsp;
            
            // Schneide bei "Themen:" ab (vor Stats/LastPost)
            var themesIndex = textAfterLink.IndexOf("Themen:", StringComparison.OrdinalIgnoreCase);
            if (themesIndex > 0)
            {
                textAfterLink = textAfterLink.Substring(0, themesIndex).Trim();
            }
            
            return textAfterLink.Trim();
        }
        
        private static string ExtractLastThreadTitle(HtmlNode cell0)
        {
            var fullText = cell0.InnerText;
            var lbIndex = fullText.IndexOf("Letzter Beitrag:", StringComparison.OrdinalIgnoreCase);
            if (lbIndex < 0) return "";
            
            var lbPart = fullText.Substring(lbIndex + 16).Trim(); // Nach "Letzter Beitrag:"
            lbPart = Regex.Replace(lbPart, @"\s+", " ");
            
            // Schneide bei "von" ab, falls da (vermeidet Überlauf)
            var vonIndex = lbPart.IndexOf("von ", StringComparison.OrdinalIgnoreCase);
            if (vonIndex > 0)
            {
                lbPart = lbPart.Substring(0, vonIndex).Trim();
            }
            
            // Entferne Forum-spezifische Anhänge, falls pattern-matchbar (z. B. "- in [Forum] wie...")
            // Hier einfach: Regex für gängige Patterns (anpassen bei Bedarf)
            lbPart = Regex.Replace(lbPart, @" -\s*in\s+[A-ZÄÖÜ][a-zäöü]+.*$", "").Trim();
            
            return lbPart;
        }
        
        private static string ExtractLastAuthor(HtmlNode cell3)
        {
            if (cell3 == null) return "";
            
            var authorText = cell3.InnerText.Trim();
            authorText = Regex.Replace(authorText, @"\s+", " ");
            
            var vonIndex = authorText.IndexOf("von ", StringComparison.OrdinalIgnoreCase);
            if (vonIndex >= 0)
            {
                authorText = authorText.Substring(vonIndex + 4).Trim();
            }
            
            // Nimm erstes Wort (Author-Name)
            var spaceIndex = authorText.IndexOf(" ");
            if (spaceIndex > 0)
            {
                authorText = authorText.Substring(0, spaceIndex).Trim();
            }
            
            // Fallback: Link-Text aus <a> in cell3
            var authorLink = cell3.SelectSingleNode(".//a");
            if (authorLink != null && string.IsNullOrEmpty(authorText))
            {
                authorText = authorLink.InnerText.Trim();
            }
            
            return authorText;
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