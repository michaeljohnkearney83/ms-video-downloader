using System.Text.Json.Serialization;
using MassTransit;

namespace MS.Vid_dldr.Manager.Messaging;

/// <summary>
/// MassTransit over RabbitMQ. The Python Downloader reads plain JSON from a non-durable queue
/// (pika queue_declare durable=False), so messages are sent as raw JSON, not in the MassTransit envelope,
/// and the queue is declared non-durable to match.
/// </summary>
public static class MessagingSetup
{
    public static Uri GetQueueAddress(string queueName) => new($"queue:{queueName}?durable=false");

    public static IServiceCollection AddDownloaderMessaging(this IServiceCollection services)
    {
        services.AddMassTransit(x =>
        {
            x.UsingRabbitMq((context, cfg) =>
            {
                var settings = context.GetRequiredService<IAppSettings>();
                cfg.Host(settings.ChannelHostName, (ushort)settings.ChannelPortNumber, "/", h =>
                {
                    h.Username(settings.ChannelUsername);
                    h.Password(settings.ChannelPassword);
                });
                ConfigureSerialization(cfg);
            });
        });

        services.AddScoped<IDownloadRequestPublisher, MassTransitDownloadRequestPublisher>();

        return services;
    }

    /// <summary>Raw JSON with the DTOs' own [JsonPropertyName]s and nulls kept, the same body DTO.ToString() gives.</summary>
    public static void ConfigureSerialization(IBusFactoryConfigurator cfg)
    {
        cfg.ConfigureJsonSerializerOptions(options =>
        {
            options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
            return options;
        });
        cfg.UseRawJsonSerializer(RawSerializerOptions.AddTransportHeaders | RawSerializerOptions.CopyHeaders, isDefault: true);
    }
}
