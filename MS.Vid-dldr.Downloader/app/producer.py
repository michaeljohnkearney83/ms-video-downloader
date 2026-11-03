import pika
import helper as h

def Send(routingKey, body, channelHost, portNumber):
    #h.printLog(f"producer.Send(routingKey: {routingKey}, body: {body}, channelHost: {channelHost}, portNumber: {portNumber})")

    connection_parameters = pika.ConnectionParameters(channelHost, portNumber)
    connection = pika.BlockingConnection(connection_parameters)
    
    channel = connection.channel()
    channel.queue_declare(queue=routingKey, durable=False, exclusive=False, auto_delete=False, arguments=None)
    channel.confirm_delivery()

    try:
        channel.basic_publish(
            exchange='',
            routing_key=routingKey,
            body=body,
            properties=pika.BasicProperties(content_type='text/plain',
                delivery_mode=pika.DeliveryMode.Persistent),
            mandatory=True)
        h.printLog(f"SUCCESS: Sent message: '{body}', on channel: '{routingKey}'.")    
    except Exception as e:
        h.printLog(f"ERROR: Sent message: '{body}', on channel: '{routingKey}', exception: '{e}'.")