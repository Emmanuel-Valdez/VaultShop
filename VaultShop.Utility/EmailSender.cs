using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Resend;

namespace UkiyoDesigns.Utility
{
    public class FakeEmailSender : IEmailSender
    {
        private readonly ILogger<FakeEmailSender> _logger;

        public FakeEmailSender(ILogger<FakeEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogWarning(
                "Fake email sender intercepted an email. Subject: {Subject}, RecipientProvided: {RecipientProvided}. The message was not sent.",
                subject,
                !string.IsNullOrWhiteSpace(email));

            return Task.CompletedTask;
        }
    }

    public class UnconfiguredEmailSender : IEmailSender
    {
        private readonly ILogger<UnconfiguredEmailSender> _logger;

        public UnconfiguredEmailSender(ILogger<UnconfiguredEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            _logger.LogError("Email sending was requested, but no production email sender is configured. Subject: {Subject}", subject);
            throw new InvalidOperationException("No production email sender is configured. Enable Email:UseFakeEmailSender for development/demo or implement a real email sender.");
        }
    }

    public class ResendEmailSender : IEmailSender
    {
        private readonly IResend _resend;
        private readonly ILogger<ResendEmailSender> _logger;
        private readonly string _fromEmail;

        public ResendEmailSender(IConfiguration configuration, ILogger<ResendEmailSender> logger)
        {
            _logger = logger;
            var apiKey = configuration["Resend:ApiKey"];
            _fromEmail = configuration["Resend:FromEmail"] ?? "onboarding@resend.dev";

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "re_xxxxxxxxx")
            {
                throw new InvalidOperationException("Missing required Resend:ApiKey configuration. Replace re_xxxxxxxxx with your real Resend API key in your private environment configuration.");
            }

            _resend = ResendClient.Create(apiKey);
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new ArgumentException("Email recipient is required.", nameof(email));
            }

            try
            {
                await _resend.EmailSendAsync(new EmailMessage()
                {
                    From = _fromEmail,
                    To = email,
                    Subject = subject,
                    HtmlBody = htmlMessage,
                });

                _logger.LogInformation("Email sent through Resend. Subject: {Subject}, RecipientProvided: {RecipientProvided}", subject, true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Resend email send failed. Subject: {Subject}, RecipientProvided: {RecipientProvided}", subject, true);
                throw;
            }
        }
    }
}
