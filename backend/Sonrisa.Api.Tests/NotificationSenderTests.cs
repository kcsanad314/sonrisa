using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Sonrisa.Api.Notifications;

namespace Sonrisa.Api.Tests;

public sealed class NotificationSenderTests
{
    [Fact]
    public async Task SlackPostsMessageToConfiguredWebhook()
    {
        Uri? requestedUri = null;
        string? postedText = null;
        using var client = new HttpClient(new StubHandler(async request =>
        {
            requestedUri = request.RequestUri;
            var json = await request.Content!.ReadAsStringAsync();
            postedText = JsonDocument.Parse(json).RootElement.GetProperty("text").GetString();
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var sender = new SlackNotificationSender(client, Settings(
            ("Notifications:Slack:WebhookUrl", "https://hooks.example.test/private-token")));

        await sender.SendAsync(new NotificationMessage("Earthquake alert", "M5.0 nearby"), CancellationToken.None);

        Assert.Equal("https://hooks.example.test/private-token", requestedUri?.ToString());
        Assert.Equal("Earthquake alert\nM5.0 nearby", postedText);
    }

    [Fact]
    public async Task SlackHttpFailureReportsStatusWithoutWebhookSecret()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        var sender = new SlackNotificationSender(client, Settings(
            ("Notifications:Slack:WebhookUrl", "https://hooks.example.test/private-token")));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendAsync(new NotificationMessage("Subject", "Body"), CancellationToken.None));

        Assert.Contains("429", error.Message);
        Assert.DoesNotContain("private-token", error.Message);
    }

    [Fact]
    public async Task MissingEmailConfigurationFailsWithoutContactingSmtp()
    {
        var sender = new EmailNotificationSender(Settings());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendAsync(new NotificationMessage("Subject", "Body"), CancellationToken.None));

        Assert.Contains("Notifications:Email:SmtpHost", error.Message);
    }

    private static IConfiguration Settings(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(item =>
            new KeyValuePair<string, string?>(item.Key, item.Value))).Build();

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => response(request);
    }
}
