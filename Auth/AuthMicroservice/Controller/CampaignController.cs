using AuthMicroservice.Model;
using AuthMicroservice.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AuthMicroservice.Controller
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CampaignController : ControllerBase
    {
        private readonly ICampaignService _campaignService;
        private readonly IApplicationService _applicationService;

        public CampaignController(ICampaignService campaignService, IApplicationService applicationService)
        {
            _campaignService = campaignService;
            _applicationService = applicationService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateCampaign([FromBody] Campaign campaign, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var audienceError = ValidateAudience(campaign);
            if (audienceError != null)
                return BadRequest(new { message = audienceError });

            var created = await _campaignService.CreateCampaignAsync(campaign, app.Id);
            return CreatedAtAction(nameof(GetCampaign), new { id = created.Id }, created);
        }

        [HttpGet]
        public async Task<IActionResult> GetCampaigns([FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaigns = await _campaignService.GetCampaignsAsync(app.Id);
            return Ok(campaigns);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetCampaign(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaign = await _campaignService.GetCampaignByIdAsync(id, app.Id);
            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCampaign(string id, [FromBody] Campaign campaign, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            if (id != campaign.Id.ToString())
                return BadRequest();

            var audienceError = ValidateAudience(campaign);
            if (audienceError != null)
                return BadRequest(new { message = audienceError });

            await _campaignService.UpdateCampaignAsync(id, campaign, app.Id);
            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCampaign(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            await _campaignService.DeleteCampaignAsync(id, app.Id);
            return NoContent();
        }

        [HttpPost("{id}/send")]
        public async Task<IActionResult> SendCampaign(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaign = await _campaignService.SendCampaignAsync(id, app.Id);
            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        [HttpPost("{id}/cancel-schedule")]
        public async Task<IActionResult> CancelSchedule(string id, [FromHeader(Name = "AppKey")] string appKey)
        {
            var (isValid, app) = await IsValidAppKey(appKey);
            if (!isValid)
                return Unauthorized(new { message = "Invalid AppKey or AppSecret" });

            var campaign = await _campaignService.CancelScheduleAsync(id, app.Id);
            if (campaign == null)
                return NotFound();

            return Ok(campaign);
        }

        private static string ValidateAudience(Campaign campaign)
        {
            if (string.Equals(campaign.TargetGroup, "segment", StringComparison.OrdinalIgnoreCase) && !campaign.SegmentId.HasValue)
                return "Choose a segment to send this campaign to.";
            return null;
        }

        private async Task<(bool, Application)> IsValidAppKey(string appKey)
        {
            var applications = await _applicationService.GetApplicationsAsync(appKey);
            var app = applications.FirstOrDefault(a => a.AppKey == appKey);
            if (app == null) return (false, null);

            // [Authorize] has already validated the JWT itself; this confirms the token's
            // own tenant (ApplicationId claim) matches the AppKey-resolved tenant for this
            // request, so a valid token for tenant A can't be replayed against tenant B's
            // AppKey.
            var applicationIdClaim = User.FindFirst("ApplicationId")?.Value;
            var isValid = !string.IsNullOrEmpty(applicationIdClaim) && applicationIdClaim == app.Id.ToString();
            return (isValid, app);
        }
    }
}
