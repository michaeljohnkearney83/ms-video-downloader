using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MS.Vid_dldr.Manager.Health;

/// <summary>
/// Checks that a worker is listening on its queue, using the RabbitMQ management API's consumer count.
/// The Downloader (Python) and the DownloadUpdater have no HTTP endpoint, but RabbitMQ knows whether anything consumes
/// their queue: no consumer means work piles up unseen. Far more consumers than any deployment runs means a leak (the
/// DownloadUpdater once registered 258,283).
/// </summary>
public sealed class RabbitMqQueueConsumersCheck : IHealthCheck
{
    public const string HttpClientName = "rabbitmq-management";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _queue;
    private readonly string _consumerName;
    private readonly int _maxExpectedConsumers;

    public RabbitMqQueueConsumersCheck(IHttpClientFactory httpClientFactory, string queue, string consumerName,
        int maxExpectedConsumers = 10)
    {
        _httpClientFactory = httpClientFactory;
        _queue = queue;
        _consumerName = consumerName;
        _maxExpectedConsumers = maxExpectedConsumers;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var failureStatus = context.Registration?.FailureStatus ?? HealthStatus.Degraded;
        var data = new Dictionary<string, object> { ["queue"] = _queue };

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            // %2F is the default virtual host "/".
            using var response = await client.GetAsync($"api/queues/%2F/{Uri.EscapeDataString(_queue)}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new HealthCheckResult(failureStatus,
                    $"Queue {_queue} does not exist: the {_consumerName} has not connected yet", data: data);

            response.EnsureSuccessStatusCode();

            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            var consumers = ReadInt(json.RootElement, "consumers");
            var messages = ReadInt(json.RootElement, "messages");
            data["consumers"] = consumers;
            data["messages"] = messages;

            if (consumers == 0)
                return new HealthCheckResult(failureStatus,
                    $"No {_consumerName} is consuming {_queue} ({messages} message(s) waiting)", data: data);

            if (consumers > _maxExpectedConsumers)
                return new HealthCheckResult(failureStatus,
                    $"{consumers} consumers on {_queue} (more than {_maxExpectedConsumers}): possible consumer leak in the {_consumerName}",
                    data: data);

            return HealthCheckResult.Healthy($"{consumers} {_consumerName} consumer(s) on {_queue}", data);
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(failureStatus,
                $"Cannot read {_queue} from the RabbitMQ management API: {ex.Message}", ex, data);
        }
    }

    // The management API can leave statistics out for a moment after a queue is declared.
    private static int ReadInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : 0;
}
