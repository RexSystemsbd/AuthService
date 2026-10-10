using AuthMicroservice.Model;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace AuthMicroservice.Service
{
    // {{variable}} substitution shared by every channel (email, SMS, WhatsApp, Messenger),
    // so a campaign written as "Hi {{firstName}}" renders the same way wherever it's sent.
    // Unknown variables are left as-is rather than blanked, which makes typos visible.
    public static class MessageTemplate
    {
        public static string Apply(string template, IReadOnlyDictionary<string, string> variables)
        {
            if (string.IsNullOrEmpty(template) || variables == null)
                return template;

            return Regex.Replace(template, @"\{\{\s*(\w+)\s*\}\}", match =>
                variables.TryGetValue(match.Groups[1].Value, out var value) ? value : match.Value);
        }

        public static Dictionary<string, string> VariablesFor(Contact contact)
        {
            var fullName = $"{contact?.FirstName} {contact?.LastName}".Trim();
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["firstName"] = contact?.FirstName ?? "",
                ["lastName"] = contact?.LastName ?? "",
                ["fullName"] = fullName,
                ["email"] = contact?.Email ?? "",
                ["phone"] = contact?.Phone ?? "",
                ["company"] = contact?.Company ?? "",
                ["city"] = contact?.City ?? "",
                ["date"] = DateTime.Now.ToString("MMMM d, yyyy"),
            };
        }

        public static string Render(string template, Contact contact, IReadOnlyDictionary<string, string> extra = null)
        {
            var variables = VariablesFor(contact);
            if (extra != null)
            {
                foreach (var pair in extra)
                    variables[pair.Key] = pair.Value;
            }
            return Apply(template, variables);
        }
    }
}
