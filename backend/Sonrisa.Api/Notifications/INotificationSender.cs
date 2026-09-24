using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Notifications;

public interface INotificationSender
{
    NotificationChannel Channel { get; }
    Task SendAsync(NotificationMessage message, CancellationToken cancellationToken);
}

public sealed record NotificationMessage(string Subject, string Body);
