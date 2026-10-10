using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuthMicroservice.Service
{
    public class TextExtractionResult
    {
        public int Characters { get; set; }
        public int EmailsFound { get; set; }
        public int PhonesFound { get; set; }
        public List<ExtractedContact> Contacts { get; set; } = new List<ExtractedContact>();
    }

    public interface IContactTextExtractionService
    {
        TextExtractionResult Extract(string text);
    }

    // Pulls contacts out of unstructured text: PDFs and text files (read to text in the
    // browser with pdf.js), vCards, email signatures, pasted lists. Two passes, merged by
    // email/phone identity:
    //   1. ContactExtractionService's ML classifier, which also finds names, companies and
    //      job titles around each email/phone;
    //   2. a sweep for every email address and phone number in the text, so a bare list
    //      ("01711..., 01822...") or a line holding nothing but a number is never dropped.
    public class ContactTextExtractionService : IContactTextExtractionService
    {
        public const int MaxCharacters = 400_000;
        private const int MaxContacts = 5000;

        private static readonly Regex EmailPattern = new Regex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", RegexOptions.Compiled);
        // Spaces/tabs only, never line breaks: a list with one number per line must not be
        // read as a single 30-digit "number".
        private static readonly Regex PhonePattern = new Regex(@"(?<![\w@])\+?\(?\d[\d\-. \t()]{6,}\d(?!\w)", RegexOptions.Compiled);
        // Dates and similar numeric shapes that the phone pattern would otherwise accept.
        private static readonly Regex DateLike = new Regex(@"^\d{1,4}[-./]\d{1,2}[-./]\d{1,4}$", RegexOptions.Compiled);

        private readonly IContactExtractionService _contactExtractionService;

        public ContactTextExtractionService(IContactExtractionService contactExtractionService)
        {
            _contactExtractionService = contactExtractionService;
        }

        public TextExtractionResult Extract(string text)
        {
            text = (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n');
            if (text.Length > MaxCharacters) text = text.Substring(0, MaxCharacters);

            var emails = EmailPattern.Matches(text).Select(m => m.Value.Trim('.')).ToList();
            var phones = FindPhones(text);

            var merged = new List<ExtractedContact>();
            var byEmail = new Dictionary<string, ExtractedContact>();
            var byPhone = new Dictionary<string, ExtractedContact>();

            void Add(ExtractedContact candidate)
            {
                var emailKey = ContactMatching.NormalizeEmail(candidate.Email);
                var phoneKey = ContactMatching.NormalizePhone(candidate.Phone);
                if (emailKey == null && phoneKey == null) return;

                ExtractedContact? target = null;
                if (emailKey != null) byEmail.TryGetValue(emailKey, out target);
                if (target == null && phoneKey != null) byPhone.TryGetValue(phoneKey, out target);
                if (target == null)
                {
                    if (merged.Count >= MaxContacts) return;
                    target = new ExtractedContact();
                    merged.Add(target);
                }

                target.Email ??= candidate.Email;
                target.Phone ??= candidate.Phone;
                target.FirstName ??= candidate.FirstName;
                target.LastName ??= candidate.LastName;
                target.Company ??= candidate.Company;
                target.JobTitle ??= candidate.JobTitle;
                target.Notes ??= candidate.Notes;
                target.RawSnippet ??= candidate.RawSnippet;

                var newEmailKey = ContactMatching.NormalizeEmail(target.Email);
                var newPhoneKey = ContactMatching.NormalizePhone(target.Phone);
                if (newEmailKey != null) byEmail[newEmailKey] = target;
                if (newPhoneKey != null) byPhone[newPhoneKey] = target;
            }

            foreach (var candidate in _contactExtractionService.ExtractContacts(text))
            {
                // The classifier accepts any 7+ digit run as a phone; hold it to the same
                // stricter rule as the sweep so dates and IDs don't become "phone numbers".
                if (candidate.Phone != null && !IsPlausiblePhone(candidate.Phone))
                    candidate.Phone = null!;
                Add(candidate);
            }

            foreach (var email in emails)
                Add(new ExtractedContact { Email = email, RawSnippet = SnippetAround(text, email) });
            foreach (var phone in phones)
                Add(new ExtractedContact { Phone = phone, RawSnippet = SnippetAround(text, phone) });

            return new TextExtractionResult
            {
                Characters = text.Trim().Length,
                EmailsFound = emails.Select(ContactMatching.NormalizeEmail).Distinct().Count(),
                PhonesFound = phones.Select(ContactMatching.NormalizePhone).Distinct().Count(),
                Contacts = merged,
            };
        }

        private static List<string> FindPhones(string text)
        {
            var phones = new List<string>();
            foreach (Match match in PhonePattern.Matches(text))
            {
                var value = match.Value.Trim();
                if (IsPlausiblePhone(value))
                {
                    phones.Add(value);
                    continue;
                }

                // Two numbers separated by a single space ("01711111111 01822222222") match
                // as one run; try the pieces on their own.
                foreach (var part in value.Split(new[] { ' ', '\t', '/' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (IsPlausiblePhone(part)) phones.Add(part.Trim());
                }
            }
            return phones;
        }

        // 9-15 digits (E.164 allows at most 15) and not shaped like a date.
        private static bool IsPlausiblePhone(string value)
        {
            var trimmed = value.Trim();
            var digits = trimmed.Count(char.IsDigit);
            return digits >= 9 && digits <= 15 && !DateLike.IsMatch(trimmed);
        }

        private static string SnippetAround(string text, string value)
        {
            var index = text.IndexOf(value, StringComparison.Ordinal);
            if (index < 0) return "";
            var start = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
            var end = text.IndexOf('\n', index + value.Length);
            if (end < 0) end = text.Length;
            var line = text.Substring(start, end - start).Trim();
            return line.Length > 200 ? line.Substring(0, 200) : line;
        }
    }
}
