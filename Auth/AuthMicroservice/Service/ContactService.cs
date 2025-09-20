using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IContactService
    {
        Task<Contact> CreateContactAsync(Contact contact, Guid applicationId);
        Task<IEnumerable<Contact>> GetContactsAsync(Guid applicationId);
        Task<Contact> GetContactByIdAsync(string id, Guid applicationId);
        Task UpdateContactAsync(string id, Contact contact, Guid applicationId);
        Task DeleteContactAsync(string id, Guid applicationId);
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
            return await _contactRepository.AddAsync(contact);
        }

        public async Task<IEnumerable<Contact>> GetContactsAsync(Guid applicationId)
        {
            var allContacts = await _contactRepository.GetAllAsync();
            return allContacts.Where(c => c.ApplicationId == applicationId);
        }

        public async Task<Contact> GetContactByIdAsync(string id, Guid applicationId)
        {
            var contact = await _contactRepository.GetByIdAsync(id);
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

            await _contactRepository.UpdateAsync(existingContact);
        }

        public async Task DeleteContactAsync(string id, Guid applicationId)
        {
            var contact = await GetContactByIdAsync(id, applicationId);
            if (contact != null)
            {
                await _contactRepository.DeleteAsync(contact.Id);
            }
        }
    }
}
