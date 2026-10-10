using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;

namespace AuthMicroservice.Service
{
    public class CrawledPage
    {
        public string Url { get; set; }
        public string Text { get; set; }
    }

    public class CrawlResult
    {
        public List<CrawledPage> Pages { get; set; } = new();
        // Contacts pulled from structured sources (mailto:/tel: links, schema.org JSON-LD)
        // rather than the ML text classifier — generally higher-confidence when present.
        public List<ExtractedContact> StructuredContacts { get; set; } = new();
    }

    public interface IWebCrawlerService
    {
        Task<(bool success, string error, CrawlResult result)> CrawlAsync(string startUrl, int maxPages, int maxDepth);
    }

    // Crawls a start URL breadth-first, following same-domain links that look like they'd
    // lead to contact info ("contact", "about", "team", ...), up to maxDepth hops and
    // maxPages total pages. For each page it both (a) returns the visible text for
    // ContactExtractionService's ML.NET classifier to run over, and (b) harvests
    // structured signals directly — mailto:/tel: links (which can carry an address that
    // never appears in the page's visible text) and schema.org Person/Organization
    // JSON-LD markup (common on modern "our team" pages and very reliable when present).
    //
    // Since this takes an arbitrary tenant-supplied URL, every hop (including redirects
    // and robots.txt fetches) is re-validated to reject private/loopback/link-local
    // addresses — an authenticated tenant could otherwise use this as an SSRF probe into
    // internal infrastructure or the cloud metadata endpoint at 169.254.169.254. It also
    // respects robots.txt Disallow rules and paces requests, since a "robust" crawler
    // means well-behaved, not just capable of reading more pages.
    public class WebCrawlerService : IWebCrawlerService
    {
        public const string HttpClientName = "WebCrawler";

        private const int MaxPageBytes = 3_000_000;
        private const int MaxTextPerPage = 200_000;
        private const int DefaultMaxPages = 10;
        private const int MaxPagesLimit = 30;
        private const int DefaultMaxDepth = 2;
        private const int MaxDepthLimit = 3;
        private const int MaxRedirectsPerPage = 5;
        private const int PolitenessDelayMs = 300;

        private static readonly string[] RelevantLinkKeywords =
        {
            "contact", "about", "team", "staff", "people", "leadership", "management", "directory", "faculty"
        };

        private static readonly Regex EmailPattern = new(
            @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$",
            RegexOptions.Compiled);

        private readonly IHttpClientFactory _httpClientFactory;

        public WebCrawlerService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<(bool success, string error, CrawlResult result)> CrawlAsync(string startUrl, int maxPages, int maxDepth)
        {
            if (!Uri.TryCreate(startUrl, UriKind.Absolute, out var startUri) || !IsHttpScheme(startUri))
                return (false, "A valid http(s) URL is required.", null);

            maxPages = Math.Clamp(maxPages <= 0 ? DefaultMaxPages : maxPages, 1, MaxPagesLimit);
            maxDepth = Math.Clamp(maxDepth <= 0 ? DefaultMaxDepth : maxDepth, 1, MaxDepthLimit);

            var client = _httpClientFactory.CreateClient(HttpClientName);
            var robotsCache = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { NormalizeUrl(startUri) };
            var queue = new Queue<(Uri uri, int depth)>();
            queue.Enqueue((startUri, 0));

            var result = new CrawlResult();
            string firstError = null;
            var isFirstRequest = true;

            while (queue.Count > 0 && result.Pages.Count < maxPages)
            {
                var (current, depth) = queue.Dequeue();

                if (!await IsPublicHostAsync(current.Host))
                {
                    firstError ??= $"{current.Host} resolves to a private or internal address, which is not allowed.";
                    continue;
                }

                if (!await IsAllowedByRobotsAsync(client, current, robotsCache))
                    continue;

                if (!isFirstRequest) await Task.Delay(PolitenessDelayMs);
                isFirstRequest = false;

                var (ok, html, error) = await FetchSafeAsync(client, current);
                if (!ok)
                {
                    firstError ??= error;
                    continue;
                }

                var doc = new HtmlDocument();
                doc.LoadHtml(html);
                doc.DocumentNode.SelectNodes("//script[@type!='application/ld+json']|//style|//noscript|//svg")?.ToList()
                    .ForEach(n => n.Remove());

                var text = ExtractVisibleText(doc);
                if (!string.IsNullOrWhiteSpace(text))
                    result.Pages.Add(new CrawledPage { Url = current.AbsoluteUri, Text = text });

                result.StructuredContacts.AddRange(ExtractStructuredContacts(doc, current.AbsoluteUri));

                if (depth < maxDepth)
                {
                    var links = ExtractLinks(doc, current)
                        .Where(l => Uri.Compare(l, startUri, UriComponents.Host, UriFormat.SafeUnescaped, StringComparison.OrdinalIgnoreCase) == 0)
                        .Where(l => RelevantLinkKeywords.Any(k => l.AbsolutePath.Contains(k, StringComparison.OrdinalIgnoreCase)));

                    foreach (var link in links)
                    {
                        var normalized = NormalizeUrl(link);
                        if (visited.Add(normalized) && Uri.TryCreate(normalized, UriKind.Absolute, out var linkUri))
                            queue.Enqueue((linkUri, depth + 1));
                    }
                }
            }

            if (result.Pages.Count == 0 && result.StructuredContacts.Count == 0)
                return (false, firstError ?? "Could not fetch any content from that URL.", null);

            return (true, null, result);
        }

        private static bool IsHttpScheme(Uri uri) =>
            uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;

        // Drops the fragment (and keeps everything else) so #section-2 style links to a
        // page already queued/visited don't get crawled as if they were separate pages.
        private static string NormalizeUrl(Uri uri) => uri.GetLeftPart(UriPartial.Query);

        private async Task<bool> IsAllowedByRobotsAsync(HttpClient client, Uri uri, Dictionary<string, List<string>> cache)
        {
            var hostKey = $"{uri.Scheme}://{uri.Authority}";
            if (!cache.TryGetValue(hostKey, out var disallowRules))
            {
                disallowRules = await FetchRobotsDisallowRulesAsync(client, hostKey);
                cache[hostKey] = disallowRules;
            }

            return !disallowRules.Any(rule => uri.AbsolutePath.StartsWith(rule, StringComparison.OrdinalIgnoreCase));
        }

        private static async Task<List<string>> FetchRobotsDisallowRulesAsync(HttpClient client, string hostKey)
        {
            var rules = new List<string>();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{hostKey}/robots.txt");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) return rules;

                var text = await response.Content.ReadAsStringAsync();
                var appliesToUs = false;
                foreach (var rawLine in text.Split('\n'))
                {
                    var line = rawLine.Split('#')[0].Trim();
                    if (line.Length == 0) continue;

                    if (line.StartsWith("User-agent:", StringComparison.OrdinalIgnoreCase))
                    {
                        appliesToUs = line["User-agent:".Length..].Trim() == "*";
                    }
                    else if (appliesToUs && line.StartsWith("Disallow:", StringComparison.OrdinalIgnoreCase))
                    {
                        var path = line["Disallow:".Length..].Trim();
                        if (path.Length > 0) rules.Add(path);
                    }
                }
            }
            catch
            {
                // No robots.txt, or couldn't fetch it — treat as "everything allowed"
                // rather than blocking the crawl over a missing/unreachable file.
            }

            return rules;
        }

        private async Task<(bool ok, string html, string error)> FetchSafeAsync(HttpClient client, Uri startingUri)
        {
            var current = startingUri;

            for (var hop = 0; hop <= MaxRedirectsPerPage; hop++)
            {
                if (!IsHttpScheme(current))
                    return (false, null, "Only http/https URLs are allowed.");

                if (!await IsPublicHostAsync(current.Host))
                    return (false, null, $"{current.Host} resolves to a private or internal address, which is not allowed.");

                using var request = new HttpRequestMessage(HttpMethod.Get, current);
                request.Headers.UserAgent.ParseAdd("NewsletterContactCrawler/1.0");
                request.Headers.Accept.ParseAdd("text/html");

                HttpResponseMessage response;
                try
                {
                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                }
                catch
                {
                    return (false, null, $"Could not reach {current}.");
                }

                using (response)
                {
                    var status = (int)response.StatusCode;
                    if (status is 301 or 302 or 303 or 307 or 308)
                    {
                        var location = response.Headers.Location;
                        if (location == null) return (false, null, $"{current} redirected without a Location header.");
                        current = location.IsAbsoluteUri ? location : new Uri(current, location);
                        continue;
                    }

                    if (!response.IsSuccessStatusCode)
                        return (false, null, $"Received status {status} from {current}.");

                    var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                    if (!contentType.Contains("html", StringComparison.OrdinalIgnoreCase) &&
                        !contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
                        return (false, null, $"{current} did not return an HTML page.");

                    var html = await ReadCappedAsync(response, MaxPageBytes);
                    return (true, html, null);
                }
            }

            return (false, null, $"Too many redirects fetching {startingUri}.");
        }

        private static async Task<string> ReadCappedAsync(HttpResponseMessage response, int maxBytes)
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                var remaining = maxBytes - (int)buffer.Length;
                if (remaining <= 0) break;
                await buffer.WriteAsync(chunk.AsMemory(0, Math.Min(read, remaining)));
                if (read > remaining) break;
            }

            var encoding = response.Content.Headers.ContentType?.CharSet is string charset
                ? TryGetEncoding(charset)
                : Encoding.UTF8;
            return encoding.GetString(buffer.ToArray());
        }

        private static Encoding TryGetEncoding(string charset)
        {
            try { return Encoding.GetEncoding(charset); }
            catch { return Encoding.UTF8; }
        }

        private static async Task<bool> IsPublicHostAsync(string host)
        {
            IPAddress[] addresses;
            if (IPAddress.TryParse(host, out var literal))
            {
                addresses = new[] { literal };
            }
            else
            {
                try
                {
                    addresses = await Dns.GetHostAddressesAsync(host);
                }
                catch
                {
                    return false;
                }
            }

            return addresses.Length > 0 && addresses.All(a => !IsBlockedAddress(a));
        }

        private static bool IsBlockedAddress(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

            if (IPAddress.IsLoopback(address)) return true;

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var b = address.GetAddressBytes();
                if (b[0] == 10) return true;                                  // 10.0.0.0/8
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true;      // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return true;                  // 192.168.0.0/16
                if (b[0] == 169 && b[1] == 254) return true;                  // 169.254.0.0/16 (incl. cloud metadata)
                if (b[0] == 0) return true;                                   // 0.0.0.0/8
                if (b[0] >= 224) return true;                                 // multicast/reserved
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast) return true;
                var b = address.GetAddressBytes();
                if ((b[0] & 0xfe) == 0xfc) return true;                       // fc00::/7 unique local
            }

            return false;
        }

        private static string ExtractVisibleText(HtmlDocument doc)
        {
            var text = WebUtility.HtmlDecode(doc.DocumentNode.InnerText ?? "");
            text = Regex.Replace(text, @"[ \t]+", " ");
            text = Regex.Replace(text, @"\n\s*\n\s*", "\n\n").Trim();
            return text.Length > MaxTextPerPage ? text[..MaxTextPerPage] : text;
        }

        private static List<Uri> ExtractLinks(HtmlDocument doc, Uri baseUri)
        {
            var links = new List<Uri>();
            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors == null) return links;

            foreach (var a in anchors)
            {
                var href = a.GetAttributeValue("href", "");
                if (string.IsNullOrWhiteSpace(href)) continue;
                if (href.StartsWith('#') || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
                    href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Uri.TryCreate(baseUri, href, out var resolved) && IsHttpScheme(resolved))
                    links.Add(resolved);
            }

            return links;
        }

        // Structured signals that don't need the ML classifier at all: mailto:/tel: link
        // targets (which can carry an address never shown as visible text — e.g. an
        // "Email me" link) and schema.org Person/Organization JSON-LD markup.
        private static List<ExtractedContact> ExtractStructuredContacts(HtmlDocument doc, string pageUrl)
        {
            var results = new List<ExtractedContact>();

            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors != null)
            {
                foreach (var a in anchors)
                {
                    var href = a.GetAttributeValue("href", "");
                    if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        var email = WebUtility.UrlDecode(href[7..].Split('?')[0]).Trim();
                        if (EmailPattern.IsMatch(email))
                        {
                            results.Add(new ExtractedContact
                            {
                                Email = email,
                                RawSnippet = $"mailto link on {pageUrl}: {WebUtility.HtmlDecode(a.InnerText ?? "").Trim()}",
                            });
                        }
                    }
                    else if (href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
                    {
                        var phone = WebUtility.UrlDecode(href[4..]).Trim();
                        if (phone.Count(char.IsDigit) >= 7)
                        {
                            results.Add(new ExtractedContact
                            {
                                Phone = phone,
                                RawSnippet = $"tel link on {pageUrl}: {WebUtility.HtmlDecode(a.InnerText ?? "").Trim()}",
                            });
                        }
                    }
                }
            }

            var ldNodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (ldNodes != null)
            {
                foreach (var node in ldNodes)
                    results.AddRange(ParseJsonLd(node.InnerText, pageUrl));
            }

            return results;
        }

        private static List<ExtractedContact> ParseJsonLd(string json, string pageUrl)
        {
            var results = new List<ExtractedContact>();
            if (string.IsNullOrWhiteSpace(json)) return results;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch
            {
                return results;
            }

            using (doc)
            {
                CollectPersonNodes(doc.RootElement, pageUrl, results);
            }

            return results;
        }

        private static void CollectPersonNodes(JsonElement element, string pageUrl, List<ExtractedContact> results)
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                    CollectPersonNodes(item, pageUrl, results);
                return;
            }

            if (element.ValueKind != JsonValueKind.Object) return;

            var type = GetJsonLdType(element);
            if (type != null && type.Contains("Person", StringComparison.OrdinalIgnoreCase))
            {
                var contact = BuildContactFromPersonNode(element, pageUrl);
                if (contact != null) results.Add(contact);
            }

            // Recurse into nested objects/arrays — e.g. a top-level Organization node with
            // an "employee" or "member" array of Person entries.
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
                    CollectPersonNodes(prop.Value, pageUrl, results);
            }
        }

        private static string GetJsonLdType(JsonElement obj)
        {
            if (!obj.TryGetProperty("@type", out var typeProp)) return null;
            if (typeProp.ValueKind == JsonValueKind.String) return typeProp.GetString();
            if (typeProp.ValueKind == JsonValueKind.Array)
                return string.Join(",", typeProp.EnumerateArray()
                    .Where(t => t.ValueKind == JsonValueKind.String)
                    .Select(t => t.GetString()));
            return null;
        }

        private static ExtractedContact BuildContactFromPersonNode(JsonElement person, string pageUrl)
        {
            var name = GetJsonLdString(person, "name");
            var email = GetJsonLdString(person, "email")?.Replace("mailto:", "", StringComparison.OrdinalIgnoreCase).Trim();
            var phone = GetJsonLdString(person, "telephone")?.Replace("tel:", "", StringComparison.OrdinalIgnoreCase).Trim();
            var jobTitle = GetJsonLdString(person, "jobTitle");
            var company = GetJsonLdOrgName(person, "worksFor");

            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone) && string.IsNullOrWhiteSpace(name))
                return null;

            var (firstName, lastName) = ContactExtractionService.SplitName(name);

            return new ExtractedContact
            {
                Email = string.IsNullOrWhiteSpace(email) ? null : email,
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                FirstName = firstName,
                LastName = lastName,
                JobTitle = jobTitle,
                Company = company,
                RawSnippet = $"schema.org Person markup on {pageUrl}",
            };
        }

        private static string GetJsonLdString(JsonElement obj, string prop)
        {
            if (!obj.TryGetProperty(prop, out var val)) return null;
            return val.ValueKind == JsonValueKind.String ? val.GetString() : null;
        }

        private static string GetJsonLdOrgName(JsonElement obj, string prop)
        {
            if (!obj.TryGetProperty(prop, out var val)) return null;
            if (val.ValueKind == JsonValueKind.String) return val.GetString();
            if (val.ValueKind == JsonValueKind.Object && val.TryGetProperty("name", out var nameVal) && nameVal.ValueKind == JsonValueKind.String)
                return nameVal.GetString();
            return null;
        }
    }
}
