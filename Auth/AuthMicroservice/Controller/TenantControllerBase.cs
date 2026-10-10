using AuthMicroservice.Service;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    // Shared AppKey/tenant check for the CRM controllers (segments, feedback, onboarding,
    // data bank). Same rule the older controllers each implement inline.
    public abstract class TenantControllerBase : ControllerBase
    {
        protected readonly IApplicationService _applicationService;

        protected TenantControllerBase(IApplicationService applicationService)
        {
            _applicationService = applicationService;
        }

        protected async Task<Application> ResolveApplicationAsync(string appKey)
        {
            if (string.IsNullOrWhiteSpace(appKey)) return null;
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            return applications.FirstOrDefault(a => a.AppKey == appKey);
        }

        protected async Task<(bool, Application)> IsValidAppKey(string appKey)
        {
            var app = await ResolveApplicationAsync(appKey);
            if (app == null) return (false, null);

            // [Authorize] has already validated the JWT itself; this confirms the token's
            // own tenant (ApplicationId claim) matches the AppKey-resolved tenant for this
            // request, so a valid token for tenant A can't be replayed against tenant B's
            // AppKey.
            var applicationIdClaim = User.FindFirst("ApplicationId")?.Value;
            var isValid = !string.IsNullOrEmpty(applicationIdClaim) && applicationIdClaim == app.Id.ToString();
            return (isValid, app);
        }

        protected IActionResult InvalidAppKey() => Unauthorized(new { message = "Invalid AppKey or AppSecret" });
    }
}
