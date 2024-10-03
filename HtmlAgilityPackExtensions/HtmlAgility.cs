using HtmlAgilityPack;
using System.Text;

namespace HtmlAgility
{
    /// <summary>
    /// Provides extension methods for the HtmlDocument class.
    /// </summary>
    public static class HtmlAgilityPackExtensions
    {
        static HttpClient _httpClient = new();

        /// <summary>
        /// Converts an HtmlDocument to its Markdown representation.
        /// </summary>
        /// <param name="htmlDocument">The HtmlDocument to convert.</param>
        /// <returns>A string containing the Markdown representation of the HtmlDocument.</returns>
        public static string ConvertToMarkdown(this HtmlDocument htmlDocument)
        {
            StringBuilder markdownBuilder = new StringBuilder();
            ConvertNodeToMarkdown(htmlDocument.DocumentNode, markdownBuilder);
            return markdownBuilder.ToString();
        }

        /// <summary>
        /// Recursively converts an HtmlNode to its Markdown representation.
        /// </summary>
        /// <param name="node">The HtmlNode to convert.</param>
        /// <param name="markdownBuilder">The StringBuilder to append the Markdown representation to.</param>
        private static void ConvertNodeToMarkdown(HtmlNode node, StringBuilder markdownBuilder)
        {
            if (node == null || markdownBuilder == null)
            {
                return;
            }

            switch (node.Name.ToLower())
            {
                case "h1":
                    markdownBuilder.AppendLine($"# {CleanText(node.InnerText)}");
                    markdownBuilder.AppendLine();
                    break;
                case "h2":
                    markdownBuilder.AppendLine($"## {CleanText(node.InnerText)}");
                    markdownBuilder.AppendLine();
                    break;
                case "h3":
                    markdownBuilder.AppendLine($"### {CleanText(node.InnerText)}");
                    markdownBuilder.AppendLine();
                    break;
                case "p":
                    markdownBuilder.AppendLine(CleanText(node.InnerText));
                    markdownBuilder.AppendLine();
                    break;
                case "a":
                    string href = node.GetAttributeValue("href", "#");
                    markdownBuilder.Append($"[{CleanText(node.InnerText)}]({href})");
                    break;
                case "img":
                    string src = node.GetAttributeValue("src", "");
                    string alt = node.GetAttributeValue("alt", "");
                    markdownBuilder.AppendLine($"![{alt}]({src})");
                    break;
                case "ul":
                    var listItems = node.SelectNodes("li");
                    if (listItems != null)
                    {
                        foreach (var li in listItems)
                        {
                            markdownBuilder.AppendLine($"* {CleanText(li.InnerText)}");
                        }
                        markdownBuilder.AppendLine();
                    }
                    break;
                case "ol":
                    int index = 1;
                    var orderedListItems = node.SelectNodes("li");
                    if (orderedListItems != null)
                    {
                        foreach (var li in orderedListItems)
                        {
                            markdownBuilder.AppendLine($"{index}. {CleanText(li.InnerText)}");
                            index++;
                        }
                        markdownBuilder.AppendLine();
                    }
                    break;
                case "b":
                case "strong":
                    markdownBuilder.Append($"**{CleanText(node.InnerText)}**");
                    break;
                case "i":
                case "em":
                    markdownBuilder.Append($"*{CleanText(node.InnerText)}*");
                    break;
                case "script":
                case "style":
                case "#comment":
                    // Ignore script, style, and comment nodes
                    break;
                default:
                    if (node.HasChildNodes)
                    {
                        foreach (var child in node.ChildNodes)
                        {
                            ConvertNodeToMarkdown(child, markdownBuilder);
                        }
                    }
                    else
                    {
                        markdownBuilder.Append(CleanText(node.InnerText));
                    }
                    break;
            }
        }

        /// <summary>
        /// Cleans the text by removing excessive whitespace and tabs.
        /// </summary>
        /// <param name="text">The text to clean.</param>
        /// <returns>The cleaned text.</returns>
        private static string CleanText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            // Replace tabs with a single space
            text = text.Replace("\t", " ");

            // Replace multiple spaces with a single space
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

            // Trim leading and trailing whitespace
            return text.Trim();
        }

        /// <summary>
        /// Reads the HTML content from the specified URL and converts it to its Markdown representation.
        /// </summary>
        /// <param name="url">The URL of the HTML page to convert.</param>
        /// <returns>A string containing the Markdown representation of the HTML page.</returns>
        public static async Task<string> ConvertUrlToMarkdownAsync(string url)
        {
            string? htmlContent;
            int timeoutInSeconds = 10; // Set your desired timeout here

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutInSeconds)))
            {
                try
                {
                    htmlContent = await _httpClient.GetStringAsync(url, cts.Token);
                }
                catch (Exception ex)
                {
                    return ex.Message;
                }
            }

            var htmlDocument = new HtmlDocument();
            try
            {
                htmlDocument.LoadHtml(htmlContent);
                return htmlDocument.ConvertToMarkdown();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(htmlContent))
                {
                    return htmlContent;
                }
                return ex.Message;
            }
        }

        /// <summary>
        /// Reads the HTML content from the specified URL and converts it to its Markdown representation.
        /// </summary>
        /// <param name="url">The URL of the HTML page to convert.</param>
        /// <returns>A string containing the Markdown representation of the HTML page.</returns>
        public static string ConvertUrlToMarkdown(string url)
        {
            string? htmlContent;
            try
            {
                htmlContent = _httpClient.GetStringAsync(url).Result;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }

            HtmlDocument htmlDocument = new HtmlDocument();
            try
            {
                htmlDocument.LoadHtml(htmlContent);
                return htmlDocument.ConvertToMarkdown();
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(htmlContent))
                {
                    return htmlContent;
                }
                return ex.Message;
            }
        }
    }
}