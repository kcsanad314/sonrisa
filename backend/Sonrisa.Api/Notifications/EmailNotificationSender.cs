using System.Net;
using System.Net.Mail;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Notifications;

public sealed class EmailNotificationSender(IConfiguration configuration) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var settings = configuration.GetSection("Notifications:Email");
        var host = Required(settings, "SmtpHost");
        var from = Required(settings, "From");
        var to = Required(settings, "To");
        var port = settings.GetValue<int?>("Port") ?? 587;
        if (port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Notifications:Email:Port must be between 1 and 65535.");
        }

        MailMessage mail;
        try
        {
            mail = new MailMessage(from, to, message.Subject, message.Body);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException("Notifications:Email:From or To is not a valid email address.");
        }

        using (mail)
        {
            using var client = new SmtpClient(host, port)
            {
                EnableSsl = settings.GetValue<bool?>("UseSsl") ?? true,
                UseDefaultCredentials = false,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 15000
            };

            var username = settings["Username"];
            if (!string.IsNullOrWhiteSpace(username))
            {
                client.Credentials = new NetworkCredential(username, Required(settings, "Password"));
            }

            try
            {
                await client.SendMailAsync(mail, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (SmtpException exception)
            {
                throw new InvalidOperationException($"SMTP send failed with status {exception.StatusCode}.", exception);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"SMTP send failed ({exception.GetType().Name}).", exception);
            }
        }
    }

    private static string Required(IConfigurationSection settings, string key) =>
        !string.IsNullOrWhiteSpace(settings[key]) ? settings[key]! :
            throw new InvalidOperationException($"Notifications:Email:{key} is not configured.");
}
