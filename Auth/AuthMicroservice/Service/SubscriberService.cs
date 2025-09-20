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
    }

    public class SubscriberService : ISubscriberService
    {
        private readonly ISubscriberRepository _subscriberRepository;

        public SubscriberService(ISubscriberRepository subscriberRepository)
        {
            _subscriberRepository = subscriberRepository;
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

            return await _subscriberRepository.AddAsync(newSubscriber);
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
}
