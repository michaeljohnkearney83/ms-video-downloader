using System.Text;
using RabbitMQ.Client;
using Serilog;

namespace MS.Vid_dldr.Manager.RabbitMq;

public class RMQProducer
{
    private readonly string _hostName;
    private readonly int _portNumber;
    private readonly string _queueName;
    private readonly ConnectionFactory _connectionFactory;

    public RMQProducer(string hostName, int portNumber, string queueName)
    {
        _hostName = hostName;
        _portNumber = portNumber;
        _queueName = queueName;

        Log.Information($"{Constants.MsNoticeLogPrefix} {_hostName} {_portNumber} {_queueName}");

        _connectionFactory = new ConnectionFactory { HostName = _hostName, Port = _portNumber };
    }

    public void Publish(string message)
    {
        Log.Information($"{Constants.MsNoticeLogPrefix}Publish(string message: {message})");

        using var connection = _connectionFactory.CreateConnection();
        using var channel = connection.CreateModel();

        channel.QueueDeclare(queue: _queueName,
                     durable: false,
                     exclusive: false,
                     autoDelete: false,
                     arguments: null);

        var encodedMessage = Encoding.UTF8.GetBytes(message);

        channel.BasicPublish("", _queueName, null, encodedMessage);
    }
}