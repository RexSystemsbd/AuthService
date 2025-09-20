using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface ISubscriberService
    {
        Task<Subscriber> SubscribeAsync(string email, string applicationId);
        Task UnsubscribeAsync(string email, string applicationId);
        Task<IEnumerable<Subscriber>> GetSubscribersAsync(string applicationId);
        Task<string> ContactWithUSAsync(ContactWithUSReqest contact, string applicationId);
    }

    public class SubscriberService : ISubscriberService
    {
        private readonly ISubscriberRepository _subscriberRepository;
        private readonly ISmtpConfigService _smtpConfigService;
        private readonly IContactRepository _contactRepository;

        public SubscriberService(IContactRepository contactRepository,ISmtpConfigService smtpConfigService, ISubscriberRepository subscriberRepository)
        {
            _subscriberRepository = subscriberRepository;
            _smtpConfigService = smtpConfigService;
            _contactRepository = contactRepository;
        }

        public async Task<Subscriber> SubscribeAsync(string email, string applicationId)
        {
            var existingSubscriber = await _subscriberRepository.GetByEmailAndApplicationIdAsync(email, applicationId);
            if (existingSubscriber != null)
            {
                existingSubscriber.IsSubscribed = true;
                await _subscriberRepository.UpdateAsync(existingSubscriber);
                return existingSubscriber;
            }

            var newSubscriber = new Subscriber
            {
                Email = email,
                IsSubscribed = true,
                ApplicationId = applicationId
            };

            await _subscriberRepository.AddAsync(newSubscriber);
            return newSubscriber;
        }

        public async Task<string> ContactWithUSAsync(ContactWithUSReqest contact, string applicationId)
        {
            var existingSubscriber = await _subscriberRepository.GetByEmailAndApplicationIdAsync(contact.Email, applicationId);
            if (existingSubscriber != null)
            {
                existingSubscriber.IsSubscribed = true;
                await _subscriberRepository.UpdateAsync(existingSubscriber);
            }
            else
            {
                var newSubscriber = new Subscriber
                {
                    Email = contact.Email,
                    IsSubscribed = true,
                    ApplicationId = applicationId
                };

                await _subscriberRepository.AddAsync(newSubscriber);
                var contactEntity = new Contact
                {
                    FirstName = contact.Name,
                    Email = contact.Email,
                    Phone = contact.PhoneNumber,
                    Company = contact.Company,
                    ApplicationId = new Guid(applicationId)
                };
                await _contactRepository.AddAsync(contactEntity);

            }

            return "Thank you for contacting us. We will get back to you shortly.";

        }

        public async Task UnsubscribeAsync(string email, string applicationId)
        {
            var subscriber = await _subscriberRepository.GetByEmailAndApplicationIdAsync(email, applicationId);
            if (subscriber != null)
            {
                subscriber.IsSubscribed = false;
                await _subscriberRepository.UpdateAsync(subscriber);
            }
        }

        public async Task<IEnumerable<Subscriber>> GetSubscribersAsync(string applicationId)
        {
            var allSubscribers = await _subscriberRepository.GetAllAsync();
            return allSubscribers.Where(s => s.ApplicationId == applicationId && s.IsSubscribed);
        }
    }


    public class ContactWithUSReqest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string Company { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
    }
}
