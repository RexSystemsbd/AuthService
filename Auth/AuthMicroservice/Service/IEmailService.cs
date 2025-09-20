using System.Collections.Generic;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IEmailService
    {
        Task SendEmailAsync(string applicationId, string subject, string body, List<string> to);
    }
}
