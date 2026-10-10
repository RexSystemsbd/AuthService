using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace AuthMicroservice.Service
{
    public class ExtractedContact
    {
        public string Email { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Company { get; set; }
        public string JobTitle { get; set; }
        public string Notes { get; set; }
        public string RawSnippet { get; set; }
    }

    public interface IContactExtractionService
    {
        List<ExtractedContact> ExtractContacts(string text);
    }

    // There is no off-the-shelf pretrained NER model for ML.NET, so name/company/job-title
    // classification here is a small multiclass text classifier trained once at startup from
    // a compact, hand-authored example set below. Email and phone are exact-format fields
    // extracted with regex instead — ML adds nothing there.
    public class ContactExtractionService : IContactExtractionService
    {
        private const int MaxInputLength = 400_000;
        private const int MaxCandidates = 1000;

        private static readonly Regex EmailPattern = new Regex(
            @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
            RegexOptions.Compiled);

        private static readonly Regex PhonePattern = new Regex(
            @"\+?\d[\d\-.\s()]{6,}\d",
            RegexOptions.Compiled);

        private static readonly Regex BlankLineSplit = new Regex(@"\n\s*\n+", RegexOptions.Compiled);

        private readonly MLContext _mlContext;
        private readonly ITransformer _model;

        public ContactExtractionService()
        {
            _mlContext = new MLContext(seed: 1);
            var trainingData = _mlContext.Data.LoadFromEnumerable(BuildTrainingExamples());

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey("Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText("Features", nameof(LineExample.Text)))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy())
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            _model = pipeline.Fit(trainingData);
        }

        public List<ExtractedContact> ExtractContacts(string text)
        {
            var results = new List<ExtractedContact>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            var trimmed = text.Length > MaxInputLength ? text.Substring(0, MaxInputLength) : text;
            var engine = _mlContext.Model.CreatePredictionEngine<LineExample, LinePrediction>(_model);

            foreach (var block in SplitIntoBlocks(trimmed))
            {
                foreach (var candidate in SplitBlockByEmail(block))
                {
                    if (results.Count >= MaxCandidates) return results;

                    var extracted = ExtractFromCandidate(candidate, engine);
                    if (extracted != null) results.Add(extracted);
                }
            }

            return results;
        }

        private static IEnumerable<string> SplitIntoBlocks(string text)
        {
            if (BlankLineSplit.IsMatch(text))
            {
                return BlankLineSplit.Split(text)
                    .Select(b => b.Trim())
                    .Where(b => b.Length > 0);
            }

            var lines = text.Split('\n')
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            // No blank-line paragraphs and no line breaks at all: treat the whole thing as one block.
            return lines.Count > 1 ? lines : new List<string> { text.Trim() };
        }

        // Most blocks contain exactly one email. When a block has more than one (e.g. a pasted
        // roster with no blank lines between entries), split its lines by nearest-line distance
        // to each email so every email still anchors its own candidate.
        private static IEnumerable<string> SplitBlockByEmail(string block)
        {
            var lines = block.Split('\n');
            var emailLineIndexes = new List<int>();
            for (var i = 0; i < lines.Length; i++)
            {
                if (EmailPattern.IsMatch(lines[i])) emailLineIndexes.Add(i);
            }

            if (emailLineIndexes.Count <= 1)
            {
                yield return block;
                yield break;
            }

            for (var e = 0; e < emailLineIndexes.Count; e++)
            {
                var start = e == 0 ? 0 : (emailLineIndexes[e - 1] + emailLineIndexes[e]) / 2 + 1;
                var end = e == emailLineIndexes.Count - 1 ? lines.Length - 1 : (emailLineIndexes[e] + emailLineIndexes[e + 1]) / 2;
                yield return string.Join('\n', lines.Skip(start).Take(end - start + 1));
            }
        }

        private ExtractedContact ExtractFromCandidate(string candidate, PredictionEngine<LineExample, LinePrediction> engine)
        {
            var rawSnippet = candidate.Length > 300 ? candidate.Substring(0, 300) : candidate;

            var emailMatch = EmailPattern.Match(candidate);
            var email = emailMatch.Success ? emailMatch.Value : null;
            var remaining = emailMatch.Success ? candidate.Remove(emailMatch.Index, emailMatch.Length) : candidate;

            var phoneMatch = PhonePattern.Match(remaining);
            string phone = null;
            if (phoneMatch.Success)
            {
                var digitCount = phoneMatch.Value.Count(char.IsDigit);
                if (digitCount >= 7)
                {
                    phone = phoneMatch.Value.Trim();
                    remaining = remaining.Remove(phoneMatch.Index, phoneMatch.Length);
                }
            }

            var lines = remaining.Split('\n')
                .Select(l => l.Trim().Trim(',', ';', '-', '|'))
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (email == null && lines.Count == 0) return null;

            string name = null, company = null, jobTitle = null;
            var notes = new List<string>();

            foreach (var line in lines)
            {
                var prediction = engine.Predict(new LineExample { Text = line });
                switch (prediction.PredictedLabel)
                {
                    case "PersonName" when name == null:
                        name = line;
                        break;
                    case "Company" when company == null:
                        company = line;
                        break;
                    case "JobTitle" when jobTitle == null:
                        jobTitle = line;
                        break;
                    default:
                        notes.Add(line);
                        break;
                }
            }

            var (firstName, lastName) = SplitName(name);

            return new ExtractedContact
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Phone = phone,
                Company = company,
                JobTitle = jobTitle,
                Notes = notes.Count > 0 ? string.Join(" | ", notes) : null,
                RawSnippet = rawSnippet,
            };
        }

        internal static (string firstName, string lastName) SplitName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return (null, null);

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return (parts[0], null);

            return (string.Join(' ', parts.Take(parts.Length - 1)), parts[^1]);
        }

        private class LineExample
        {
            public string Text { get; set; }
            public string Label { get; set; }
        }

        private class LinePrediction
        {
            [ColumnName("PredictedLabel")]
            public string PredictedLabel { get; set; }
        }

        private static IEnumerable<LineExample> BuildTrainingExamples()
        {
            var personNames = new[]
            {
                "John Smith", "Jane Doe", "Michael Johnson", "Sarah Williams", "David Brown",
                "Emily Davis", "Robert Miller", "Jessica Wilson", "James Moore", "Linda Taylor",
                "William Anderson", "Patricia Thomas", "Richard Jackson", "Barbara White", "Joseph Harris",
                "Susan Martin", "Thomas Thompson", "Karen Garcia", "Charles Martinez", "Nancy Robinson",
                "Christopher Clark", "Lisa Rodriguez", "Daniel Lewis", "Betty Lee", "Matthew Walker",
                "Margaret Hall", "Anthony Allen", "Sandra Young", "Mark King", "Ashley Wright",
                "Donald Scott", "Kimberly Green", "Steven Baker", "Emily Adams", "Paul Nelson",
                "Donna Hill", "Andrew Carter", "Michelle Mitchell", "Joshua Perez", "Carol Roberts",
                "Kenneth Turner", "Amanda Phillips", "Kevin Campbell", "Melissa Parker", "Brian Evans",
                "Deborah Edwards", "George Collins", "Stephanie Stewart", "Edward Sanchez", "Rachel Morris",
                "Jason Rogers", "Sajidur Rahman", "Md Alamin", "Rahat Hossain", "Shabik Yeamin Shoumik",
                "Pavel Md Yasin", "Jane A. Doe", "Dr. Robert Lee", "Mr. John Carter", "Ms. Alice Brown",
                "Mohammed Karim", "Fatima Begum", "Abdul Rahim", "Nusrat Jahan", "Tanvir Ahmed",
            };

            var companies = new[]
            {
                "Acme Corporation", "Acme Inc.", "Acme Ltd", "Globex Corp", "Initech LLC",
                "Umbrella Corporation", "Stark Industries", "Wayne Enterprises", "Wonka Industries", "Hooli Inc.",
                "Massive Dynamic", "Cyberdyne Systems", "Soylent Corp", "Tech Solutions Ltd", "Bright Future Enterprises",
                "Green Valley Farms", "Blue Ocean Shipping", "Sunrise Bakery", "Homex Pro Solutions", "Bonik Ltd",
                "Newsletter Co", "Prime Logistics Inc", "Apex Consulting Group", "Silverline Technologies", "Redwood Partners LLC",
                "Northstar Media", "Falcon Software Pvt Ltd", "Crescent Bank plc", "Horizon Marketing Agency", "Vertex Manufacturing Co",
                "Pioneer Insurance Group", "Emerald Trading Company", "Skyline Constructions Ltd", "Bengal Textiles Ltd", "Dhaka Software House",
                "Grameen Digital Ltd", "Orion Data Systems", "Meridian Healthcare Group", "Atlas Freight Solutions", "Quantum Analytics Inc",
            };

            var jobTitles = new[]
            {
                "Senior Software Engineer", "Marketing Manager", "Chief Executive Officer", "Product Manager", "Sales Director",
                "Human Resources Manager", "Chief Financial Officer", "Operations Manager", "Business Development Manager", "Customer Success Manager",
                "Lead Data Scientist", "Frontend Developer", "Backend Developer", "Full Stack Developer", "Project Manager",
                "Account Executive", "Regional Sales Manager", "Vice President of Engineering", "Head of Marketing", "Chief Technology Officer",
                "Executive Assistant", "Office Manager", "Financial Analyst", "Graphic Designer", "UX Designer",
                "Quality Assurance Engineer", "DevOps Engineer", "IT Support Specialist", "Content Writer", "Social Media Manager",
                "Recruiter", "Talent Acquisition Specialist", "Legal Counsel", "Supply Chain Manager", "Warehouse Supervisor",
                "Store Manager", "Branch Manager", "Managing Director", "Founder & CEO", "Co-Founder",
            };

            var other = new[]
            {
                "Best regards,", "Thanks,", "Thank you!", "Sincerely,", "Kind regards,",
                "Cheers,", "Talk soon,", "Please let me know if you have any questions.", "Looking forward to hearing from you.", "Call me anytime.",
                "123 Main Street, Suite 400", "P.O. Box 1234", "Confidential - do not forward", "This email and any attachments are confidential.", "Sent from my iPhone",
                "www.example.com", "Follow us on social media", "Visit our website for more information", "All rights reserved.", "Copyright 2026",
                "Have a great day!", "Hope you're doing well.", "Just checking in.", "Let's schedule a call next week.", "Attached is the document you requested.",
                "Please find attached my resume.", "I hope this email finds you well.", "Warm regards,", "Yours truly,", "Best,",
                "With gratitude,", "Hi there,", "Hello,", "Dear Sir/Madam,", "To whom it may concern,",
                "***", "---", "===", "Office: 3rd Floor, Gulshan Avenue", "www.homexprosolutions.com",
            };

            return personNames.Select(t => new LineExample { Text = t, Label = "PersonName" })
                .Concat(companies.Select(t => new LineExample { Text = t, Label = "Company" }))
                .Concat(jobTitles.Select(t => new LineExample { Text = t, Label = "JobTitle" }))
                .Concat(other.Select(t => new LineExample { Text = t, Label = "Other" }));
        }
    }
}
