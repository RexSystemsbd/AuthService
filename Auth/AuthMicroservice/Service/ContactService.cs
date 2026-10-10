using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public class BulkImportResult
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public int Skipped { get; set; }
    }

    public interface IContactService
    {
        Task<Contact> CreateContactAsync(Contact contact, Guid applicationId);
        Task<IEnumerable<Contact>> GetContactsAsync(Guid applicationId);
        Task<Contact> GetContactByIdAsync(string id, Guid applicationId);
        Task UpdateContactAsync(string id, Contact contact, Guid applicationId);
        Task DeleteContactAsync(string id, Guid applicationId);
        Task<BulkImportResult> BulkImportContactsAsync(List<Contact> rows, Guid applicationId);
    }

    public class ContactService : IContactService
    {
        private readonly IContactRepository _contactRepository;

        public ContactService(IContactRepository contactRepository)
        {
            _contactRepository = contactRepository;
        }

        public async Task<Contact> CreateContactAsync(Contact contact, Guid applicationId)
        {
            var existingContact = await _contactRepository.GetByEmailAndApplicationIdAsync(contact.Email, applicationId);
            if (existingContact != null)
            {
                // Decide how to handle duplicates, e.g., throw an exception or update existing.
                throw new System.Exception("A contact with this email already exists.");
            }

            contact.ApplicationId = applicationId;
            await _contactRepository.AddAsync(contact);
            return contact;
        }

        public async Task<IEnumerable<Contact>> GetContactsAsync(Guid applicationId)
        {
            var allContacts = await _contactRepository.GetAllAsync();
            return allContacts.Where(c => c.ApplicationId == applicationId);
        }

        public async Task<Contact> GetContactByIdAsync(string id, Guid applicationId)
        {
            var contact = await _contactRepository.GetByIdAsync(new Guid(id));
            if (contact == null || contact.ApplicationId != applicationId)
            {
                return null;
            }
            return contact;
        }

        public async Task UpdateContactAsync(string id, Contact contact, Guid applicationId)
        {
            var existingContact = await GetContactByIdAsync(id, applicationId);
            if (existingContact == null)
            {
                // Or handle as a "not found" case.
                return;
            }

            // Update properties
            existingContact.FirstName = contact.FirstName;
            existingContact.LastName = contact.LastName;
            existingContact.Email = contact.Email;
            existingContact.Phone = contact.Phone;
            existingContact.Company = contact.Company;
            existingContact.JobTitle = contact.JobTitle;
            existingContact.Department = contact.Department;
            existingContact.TopicsOfInterest = contact.TopicsOfInterest;
            existingContact.Source = contact.Source;
            existingContact.Notes = contact.Notes;
            existingContact.Category = contact.Category;
            existingContact.Grade = contact.Grade;
            existingContact.Industry = contact.Industry;
            existingContact.Country = contact.Country;
            existingContact.City = contact.City;
            existingContact.TimeZone = contact.TimeZone;
            existingContact.IsSubscribed = contact.IsSubscribed;
            existingContact.SubscribedDate = contact.SubscribedDate;
            existingContact.UnsubscribedDate = contact.UnsubscribedDate;
            existingContact.EngagementScore = contact.EngagementScore;
            existingContact.LastInteractionDate = contact.LastInteractionDate;
            existingContact.PreferredLanguage = contact.PreferredLanguage;
            existingContact.Status = contact.Status;
            existingContact.Nid = contact.Nid;
            existingContact.HomeAddress = contact.HomeAddress;
            existingContact.WorkAddress = contact.WorkAddress;
            existingContact.District = contact.District;
            existingContact.Division = contact.Division;
            existingContact.Thana = contact.Thana;
            existingContact.Gps = contact.Gps;
            existingContact.HealthData = contact.HealthData;
            existingContact.ExtraData = contact.ExtraData;
            existingContact.NextContactDate = contact.NextContactDate;
            existingContact.Tags = contact.Tags;

            await _contactRepository.UpdateAsync(existingContact);
        }

        public async Task DeleteContactAsync(string id, Guid applicationId)
        {
            var contact = await GetContactByIdAsync(id, applicationId);
            if (contact != null)
            {
                await _contactRepository.RemoveAsync(contact);
            }
        }

        // Matches incoming rows against existing contacts (and against each other within
        // the same batch) by EITHER normalized email OR normalized phone — not just email.
        // This matters for crawled/scraped data in particular: the same person often turns
        // up on one page with just an email and on another with just a phone number, and
        // without a phone-based match those would otherwise land as two separate contacts.
        public async Task<BulkImportResult> BulkImportContactsAsync(List<Contact> rows, Guid applicationId)
        {
            var result = new BulkImportResult();

            var existingContacts = (await _contactRepository.FindAsync(c => c.ApplicationId == applicationId)).ToList();
            var existingByEmail = BuildEmailIndex(existingContacts);
            var existingByPhone = BuildPhoneIndex(existingContacts);

            var toCreate = new List<Contact>();
            var stagedByEmail = new Dictionary<string, Contact>();
            var stagedByPhone = new Dictionary<string, Contact>();
            var updatedIds = new HashSet<Guid>();

            foreach (var row in rows)
            {
                if (row == null)
                {
                    result.Skipped++;
                    continue;
                }

                var emailKey = ContactMatching.NormalizeEmail(row.Email);
                var phoneKey = ContactMatching.NormalizePhone(row.Phone);

                if (emailKey == null && phoneKey == null)
                {
                    result.Skipped++;
                    continue;
                }

                var existing = LookUp(existingByEmail, existingByPhone, emailKey, phoneKey);
                if (existing != null)
                {
                    MergeContactFields(existing, row);
                    if (updatedIds.Add(existing.Id))
                        await _contactRepository.UpdateAsync(existing);

                    // Re-index in case this row supplied an identifier (email or phone)
                    // the existing contact didn't have before, so a later row in the same
                    // batch that only shares the *new* identifier still matches it.
                    IndexContact(existing, existingByEmail, existingByPhone);
                    continue;
                }

                var staged = LookUp(stagedByEmail, stagedByPhone, emailKey, phoneKey);
                if (staged != null)
                {
                    MergeContactFields(staged, row);
                    IndexContact(staged, stagedByEmail, stagedByPhone);
                    continue;
                }

                row.Id = Guid.NewGuid();
                row.ApplicationId = applicationId;
                if (row.Email != null) row.Email = row.Email.Trim();
                toCreate.Add(row);
                IndexContact(row, stagedByEmail, stagedByPhone);
            }

            if (toCreate.Count > 0)
            {
                await _contactRepository.AddRangeAsync(toCreate);
            }

            result.Created = toCreate.Count;
            result.Updated = updatedIds.Count;
            return result;
        }

        private static Dictionary<string, Contact> BuildEmailIndex(IEnumerable<Contact> contacts) =>
            contacts.Where(c => ContactMatching.NormalizeEmail(c.Email) != null)
                .GroupBy(c => ContactMatching.NormalizeEmail(c.Email))
                .ToDictionary(g => g.Key, g => g.First());

        private static Dictionary<string, Contact> BuildPhoneIndex(IEnumerable<Contact> contacts) =>
            contacts.Where(c => ContactMatching.NormalizePhone(c.Phone) != null)
                .GroupBy(c => ContactMatching.NormalizePhone(c.Phone))
                .ToDictionary(g => g.Key, g => g.First());

        private static Contact LookUp(Dictionary<string, Contact> byEmail, Dictionary<string, Contact> byPhone, string emailKey, string phoneKey)
        {
            if (emailKey != null && byEmail.TryGetValue(emailKey, out var byEmailMatch)) return byEmailMatch;
            if (phoneKey != null && byPhone.TryGetValue(phoneKey, out var byPhoneMatch)) return byPhoneMatch;
            return null;
        }

        private static void IndexContact(Contact contact, Dictionary<string, Contact> byEmail, Dictionary<string, Contact> byPhone)
        {
            var emailKey = ContactMatching.NormalizeEmail(contact.Email);
            var phoneKey = ContactMatching.NormalizePhone(contact.Phone);
            if (emailKey != null) byEmail[emailKey] = contact;
            if (phoneKey != null) byPhone[phoneKey] = contact;
        }

        private static void MergeContactFields(Contact existing, Contact incoming)
        {
            existing.FirstName = FirstNonEmpty(incoming.FirstName, existing.FirstName);
            existing.LastName = FirstNonEmpty(incoming.LastName, existing.LastName);
            existing.Phone = FirstNonEmpty(incoming.Phone, existing.Phone);
            existing.Company = FirstNonEmpty(incoming.Company, existing.Company);
            existing.JobTitle = FirstNonEmpty(incoming.JobTitle, existing.JobTitle);
            existing.Department = FirstNonEmpty(incoming.Department, existing.Department);
            existing.TopicsOfInterest = FirstNonEmpty(incoming.TopicsOfInterest, existing.TopicsOfInterest);
            existing.Source = FirstNonEmpty(incoming.Source, existing.Source);
            existing.Notes = FirstNonEmpty(incoming.Notes, existing.Notes);
            existing.Status = FirstNonEmpty(incoming.Status, existing.Status);
            existing.Category = FirstNonEmpty(incoming.Category, existing.Category);
            existing.Grade = FirstNonEmpty(incoming.Grade, existing.Grade);
            existing.Industry = FirstNonEmpty(incoming.Industry, existing.Industry);
            existing.Country = FirstNonEmpty(incoming.Country, existing.Country);
            existing.City = FirstNonEmpty(incoming.City, existing.City);
            existing.TimeZone = FirstNonEmpty(incoming.TimeZone, existing.TimeZone);
            existing.PreferredLanguage = FirstNonEmpty(incoming.PreferredLanguage, existing.PreferredLanguage);
            existing.Nid = FirstNonEmpty(incoming.Nid, existing.Nid);
            existing.HomeAddress = FirstNonEmpty(incoming.HomeAddress, existing.HomeAddress);
            existing.WorkAddress = FirstNonEmpty(incoming.WorkAddress, existing.WorkAddress);
            existing.District = FirstNonEmpty(incoming.District, existing.District);
            existing.Division = FirstNonEmpty(incoming.Division, existing.Division);
            existing.Thana = FirstNonEmpty(incoming.Thana, existing.Thana);
            existing.Gps = FirstNonEmpty(incoming.Gps, existing.Gps);
            existing.HealthData = FirstNonEmpty(incoming.HealthData, existing.HealthData);
            existing.ExtraData = FirstNonEmpty(incoming.ExtraData, existing.ExtraData);
            if (incoming.EngagementScore.HasValue)
                existing.EngagementScore = incoming.EngagementScore;
            // Tags accumulate across imports rather than being replaced.
            var mergedTags = SegmentService.SplitTags(existing.Tags).Union(SegmentService.SplitTags(incoming.Tags), StringComparer.OrdinalIgnoreCase).ToList();
            existing.Tags = mergedTags.Count == 0 ? null : string.Join(", ", mergedTags);
        }

        private static string FirstNonEmpty(string incoming, string current) =>
            string.IsNullOrWhiteSpace(incoming) ? current : incoming;
    }
}
