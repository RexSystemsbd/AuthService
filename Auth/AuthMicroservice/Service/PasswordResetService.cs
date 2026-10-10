using AuthMicroservice.Model;
using AuthMicroservice.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AuthMicroservice.Service
{
    public interface IPasswordResetService
    {
        // Emails a reset link if an active account with this email exists. Callers must
        // respond identically either way, so the endpoint can't be used to probe accounts.
        Task RequestResetAsync(Application application, string email);
        Task<(bool ok, string error)> ResetAsync(Application application, string token, string newPassword);
    }

    // Self-service "forgot password": a one-time link emailed to the account's address.
    // Only a SHA-256 hash of the token is stored (User.ResetPasswordToken), so a database
    // leak doesn't hand out working reset links.
    public class PasswordResetService : IPasswordResetService
    {
        public const int MinPasswordLength = 8;
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);
        // Don't send another email while a link issued within this window is still valid,
        // so repeated submissions can't be used to flood someone's inbox.
        private static readonly TimeSpan ResendCooldown = TimeSpan.FromMinutes(2);

        private readonly IUserRepository _userRepository;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PasswordResetService> _logger;
        private readonly IPasswordHasher<User> _passwordHasher = new PasswordHasher<User>();

        public PasswordResetService(IUserRepository userRepository, IEmailService emailService, IConfiguration configuration, ILogger<PasswordResetService> logger)
        {
            _userRepository = userRepository;
            _emailService = emailService;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task RequestResetAsync(Application application, string email)
        {
            var normalizedEmail = email?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedEmail)) return;

            // Same lookup login uses, so the reset applies to the account that signs in.
            var user = (await _userRepository.FindAsync(u => u.Email == normalizedEmail && u.ApplicationId == application.Id)).FirstOrDefault();
            if (user == null || string.Equals(user.Status, "inactive", StringComparison.OrdinalIgnoreCase))
                return;

            var now = DateTime.UtcNow;
            if (user.ResetPasswordTokenExpiry.HasValue && user.ResetPasswordTokenExpiry.Value - TokenLifetime + ResendCooldown > now)
                return;

            var token = GenerateToken();
            user.ResetPasswordToken = HashToken(token);
            user.ResetPasswordTokenExpiry = now.Add(TokenLifetime);
            await _userRepository.UpdateAsync(user);

            try
            {
                await _emailService.SendTransactionalEmailAsync(application.Id, "Reset your password", BuildEmail(BuildLink(token), application.Name), user.Email);
            }
            catch (Exception ex)
            {
                // Logged, not surfaced: the response must look the same whether or not the
                // account exists. Clear the token so a retry isn't blocked by the cooldown.
                _logger.LogError(ex, "Failed to send password reset email for application {ApplicationId}.", application.Id);
                user.ResetPasswordToken = null;
                user.ResetPasswordTokenExpiry = null;
                await _userRepository.UpdateAsync(user);
            }
        }

        public async Task<(bool ok, string error)> ResetAsync(Application application, string token, string newPassword)
        {
            const string invalidLink = "This reset link is invalid or has expired. Request a new one.";
            if (string.IsNullOrWhiteSpace(token))
                return (false, invalidLink);
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinPasswordLength)
                return (false, $"Password must be at least {MinPasswordLength} characters.");

            var tokenHash = HashToken(token.Trim());
            var user = (await _userRepository.FindAsync(u => u.ResetPasswordToken == tokenHash && u.ApplicationId == application.Id)).FirstOrDefault();
            if (user == null || !user.ResetPasswordTokenExpiry.HasValue || user.ResetPasswordTokenExpiry.Value < DateTime.UtcNow)
                return (false, invalidLink);

            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
            // Single use: the link stops working as soon as it has been used once.
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiry = null;
            await _userRepository.UpdateAsync(user);
            return (true, null);
        }

        private static string GenerateToken() =>
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');

        private static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private string BuildLink(string token)
        {
            var baseUrl = _configuration["Frontend:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
            return $"{baseUrl}/reset-password?token={Uri.EscapeDataString(token)}";
        }

        private static string BuildEmail(string link, string appName)
        {
            var safeLink = WebUtility.HtmlEncode(link);
            var safeAppName = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(appName) ? "your account" : appName);
            return $@"
<!DOCTYPE html>
<html>
<body style='margin:0; padding:0; font-family: Arial, Helvetica, sans-serif; background-color:#f5f5f5;'>
    <div style='max-width:600px; margin:20px auto; background-color:#ffffff; padding:24px; border-radius:6px;'>
        <h2 style='margin-top:0; color:#333;'>Reset your password</h2>
        <p style='color:#555;'>We received a request to reset the password for {safeAppName}. Click the button below to choose a new one.</p>
        <p style='margin:24px 0;'>
            <a href='{safeLink}' style='background-color:#2563eb; color:#ffffff; padding:12px 20px; border-radius:6px; text-decoration:none; font-weight:bold;'>Reset password</a>
        </p>
        <p style='color:#888; font-size:12px;'>If the button doesn't work, copy and paste this link into your browser:<br/>{safeLink}</p>
        <p style='color:#888; font-size:12px;'>This link expires in 1 hour and can only be used once. If you didn't ask to reset your password, you can ignore this email; your password won't change.</p>
    </div>
</body>
</html>";
        }
    }
}
