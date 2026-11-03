using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;

namespace MS.Vid_dldr.Manager.RabbitMq;

public class RMQConsumer
{
    private readonly string _hostName;
    private readonly string _queueName;

    public RMQConsumer(string hostName, string queueName)
    {
        _hostName = hostName;
        _queueName = queueName;
    }

    public void Subscribe()
    {
        var connectionFactory = new ConnectionFactory { HostName = _hostName };
        using var connection = connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(
            queue: _queueName,
            durable: false,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var consumer = new EventingBasicConsumer(channel);

        consumer.Received += (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            Log.Information($"{Constants.MsNoticeLogPrefix}Message Received: {message}");
        };

        channel.BasicConsume(
            queue: _queueName,
            autoAck: true,
            consumer: consumer);

        Console.ReadKey();
    }
}