using System.Net.Http.Json;
using Sonrisa.Api.Data.Entities;

namespace Sonrisa.Api.Notifications;

public sealed class SlackNotificationSender(HttpClient client, IConfiguration configuration) : INotificationSender
{
    public NotificationChannel Channel => NotificationChannel.Slack;

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken)
    {
        var webhookUrl = configuration["Notifications:Slack:WebhookUrl"];
        if (!Uri.TryCreate(webhookUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Notifications:Slack:WebhookUrl must be a configured HTTPS URL.");
        }

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(uri,
                new { text = $"{message.Subject}\n{message.Body}" }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception)
        {
            throw new TimeoutException("Slack webhook request timed out.", exception);
        }
        catch (HttpRequestException exception)
        {
            throw new InvalidOperationException("Slack webhook transport request failed.", exception);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"Slack webhook returned HTTP {(int)response.StatusCode}.");
            }
        }
    }
}
