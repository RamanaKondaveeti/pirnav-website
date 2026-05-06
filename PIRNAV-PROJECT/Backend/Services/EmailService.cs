using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Pirnav.API.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            var message = CreateMessage(toEmail, subject);
            message.Body = new TextPart("html") { Text = body };

            using var client = new SmtpClient();
            client.CheckCertificateRevocation = false;

            await client.ConnectAsync(GetSmtpServer(), GetPort(), GetSocketOptions());
            await client.AuthenticateAsync(GetSenderEmail(), GetPassword());
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Console.WriteLine($"Email sent successfully to {toEmail}");
        }

        public async Task SendEmailWithAttachmentAsync(
            string toEmail,
            string subject,
            string body,
            string filePath)
        {
            var message = CreateMessage(toEmail, subject);

            var builder = new BodyBuilder
            {
                HtmlBody = body
            };

            if (File.Exists(filePath))
            {
                builder.Attachments.Add(filePath);
            }

            message.Body = builder.ToMessageBody();

            using var client = new SmtpClient();
            client.CheckCertificateRevocation = false;

            await client.ConnectAsync(GetSmtpServer(), GetPort(), GetSocketOptions());
            await client.AuthenticateAsync(GetSenderEmail(), GetPassword());
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private MimeMessage CreateMessage(string toEmail, string subject)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                throw new InvalidOperationException("Email recipient is missing.");
            }

            var senderEmail = GetSenderEmail();
            var senderName = _configuration["EmailSettings:SenderName"];

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                string.IsNullOrWhiteSpace(senderName) ? "Pirnav" : senderName.Trim(),
                senderEmail
            ));
            message.To.Add(new MailboxAddress("", toEmail.Trim()));
            message.Subject = subject;

            return message;
        }

        private string GetSmtpServer() => GetRequiredSetting("SmtpServer");

        private string GetSenderEmail() => GetRequiredSetting("SenderEmail");

        private string GetPassword() => GetRequiredSetting("Password");

        private int GetPort()
        {
            var value = GetRequiredSetting("Port");

            if (!int.TryParse(value, out var port) || port <= 0)
            {
                throw new InvalidOperationException("EmailSettings:Port is missing or invalid.");
            }

            return port;
        }

        private SecureSocketOptions GetSocketOptions()
        {
            var configured = _configuration["EmailSettings:SecureSocketOptions"];

            if (!string.IsNullOrWhiteSpace(configured) &&
                Enum.TryParse<SecureSocketOptions>(configured, true, out var socketOptions))
            {
                return socketOptions;
            }

            return GetPort() == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
        }

        private string GetRequiredSetting(string key)
        {
            var value = _configuration[$"EmailSettings:{key}"];

            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"EmailSettings:{key} is missing. Configure it in appsettings.json or as EmailSettings__{key} in deployment.");
            }

            return value.Trim();
        }
    }
}
