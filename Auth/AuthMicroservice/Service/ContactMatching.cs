using System.Linq;

namespace AuthMicroservice.Service
{
    // Shared identity rules for deduping contacts by email or phone — used both when
    // bulk-importing into the database (ContactService) and when merging raw crawl
    // candidates from different pages/sources before they're ever shown to the user
    // (ContactController).
    public static class ContactMatching
    {
        public static string NormalizeEmail(string email) =>
            string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

        // Crawled/scraped phone numbers show up in wildly inconsistent formats
        // (+8801712345678, 01712345678, 880-1712-345678, ...). Comparing on the last 10
        // digits collapses country-code/leading-zero variants of the same number onto one
        // identity for dedup purposes, at the cost of (very rare) false-positive merges
        // across differently-formatted numbers that happen to share the same last 10
        // digits — an acceptable trade-off for a dedup heuristic, not a precision lookup.
        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return null;
            var digits = new string(phone.Where(char.IsDigit).ToArray());
            if (digits.Length < 7) return null;
            return digits.Length > 10 ? digits[^10..] : digits;
        }
    }
}
