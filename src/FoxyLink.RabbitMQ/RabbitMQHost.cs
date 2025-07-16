using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FoxyLink.RabbitMQ;

public class RabbitMQHost : QueueHost
{
    private readonly RabbitMQHostOptions _options;

    private const string _classicQueue = "classic";
    private const string _queueType = "x-queue-type";
    private const string _quorumQueue = "quorum";

    private const string _invalidQueue = @"foxylink.invalid";
    private const string _deadLetterQueue = @"foxylink.dead.letter";
    private const string _deadLetterExchange = @"foxylink.dead.letter";

    private bool disposed = false;
    private List<IChannel> _channels = [];
    private List<IConnection> _connections = new();

    public RabbitMQHost()
        : this(new RabbitMQHostOptions())
    {
    }

    public RabbitMQHost(RabbitMQHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    public async Task CreateConnectionAsync()
    {
        await SetUpQueues();

        var factory = ConnectionFactory();
        foreach (var queue in _options.Queues)
        {
            for (var i = 0; i < queue.NodesCount; i++)
            {
                var connection = await factory.CreateConnectionAsync();
                _connections.Add(connection);

                var channel = await connection.CreateChannelAsync();
                _channels.Add(channel);

                await channel.BasicQosAsync(0, queue.PrefetchCount, false);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (o, ea) =>
                {
                    try
                    {
                        await SendMessageToEndpoint(ea, queue.Name);
                    }
                    catch (Exception ex)
                    {
                        await MoveToInvalidQueue(ea, ex.ToString());
                    }
                    finally
                    {
                        await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag,
                            multiple: false);
                    }
                };
                await channel.BasicConsumeAsync(consumer: consumer, queue: queue.Name, autoAck: false);
            }
        }
    }

    private ConnectionFactory ConnectionFactory()
    {
        return new ConnectionFactory()
        {
            AutomaticRecoveryEnabled = true,
            //DispatchConsumersAsync = true,
            TopologyRecoveryEnabled = true,
            Uri = new Uri(_options.AmqpUri)
        };
    }

    private async Task SetUpQueues()
    {
        var factory = ConnectionFactory();
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(_invalidQueue, true, false, false, null);

        await channel.ExchangeDeclareAsync(_deadLetterExchange, @"direct", true, false);
        await channel.QueueDeclareAsync(_deadLetterQueue, true, false, false, null);
        await channel.QueueBindAsync(_deadLetterQueue, _deadLetterExchange, _deadLetterQueue);

        foreach (var queue in _options.Queues)
        {
            await channel.QueueDeclareAsync(queue.Name, true, false, false, 
                new Dictionary<string, object>
                {
                    { "x-dead-letter-exchange", _deadLetterExchange },
                    { "x-dead-letter-routing-key", _deadLetterQueue },
                    { _queueType, queue.QuorumQueue ? _quorumQueue : _classicQueue }
                });

            if (_options.RetryInMilliseconds.Count > 0)
            {
                var retryQueue = $"{queue.Name}.retry";
                await channel.QueueDeclareAsync(retryQueue, true, false, false,
                    new Dictionary<string, object>
                    {
                        { "x-dead-letter-exchange", "" },
                        { "x-dead-letter-routing-key", queue.Name },
                        { _queueType, queue.QuorumQueue ? _quorumQueue : _classicQueue }
                    });
                await channel.QueueBindAsync(queue.Name, _deadLetterExchange, queue.Name);
            }
        }
    }

    private async Task MoveToRetryQueue(BasicDeliverEventArgs ea, string queue, string message)
    {
        var attempt = 1;
        var retries = _options.RetryInMilliseconds;
        if (retries.Count == 0)
        {
            await MoveToDeadLetterQueue(ea, $"The number of retry attempts isn't set. {message}");
            return;
        }

        if (ea.BasicProperties.Headers?.ContainsKey("RetryAttempts") ?? false)
        {
            if (int.TryParse(ea.BasicProperties.Headers["RetryAttempts"].ToString(), out attempt))
            {
                attempt++;
            }
        }

        if (attempt > retries.Count)
        {
            await MoveToDeadLetterQueue(ea, $"Message has exceeded the maximum number of retry attempts. {message}");
            return;
        }

        var factory = ConnectionFactory();
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        var props = CreatePropertiesFromEvent(ea, attempt, message);
        await channel.BasicPublishAsync(exchange: "",
            routingKey: $"{queue}.retry", 
            mandatory: false,
            basicProperties: props,
            body: ea.Body);
    }

    private async Task MoveToDeadLetterQueue(BasicDeliverEventArgs ea, string message)
    {
        var props = CreatePropertiesFromEvent(ea);
        props.Headers.Add("msg-dead-letter", JsonSerializer.Serialize(
            new { success = false, log = message }));

        await MoveToServiceQueue(_deadLetterExchange, _deadLetterQueue, props, ea.Body.ToArray());
    }

    private async Task MoveToInvalidQueue(BasicDeliverEventArgs ea, string message)
    {
        var props = CreatePropertiesFromEvent(ea);
        props.Headers.Add("msg-invalid-error", JsonSerializer.Serialize(
            new { success = false, log = message }));

        await MoveToServiceQueue("", _invalidQueue, props, ea.Body.ToArray());
    }

    private async Task MoveToServiceQueue(string exchange, string routingKey, BasicProperties props, byte[] body)
    {
        var factory = ConnectionFactory();
        using var connection = await factory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        if (props.ReplyTo != null)
        {
            await channel.BasicPublishAsync(exchange: "",
                routingKey: props.ReplyTo,
                mandatory: false,
                basicProperties: props,
                body: body);
        }

        await channel.BasicPublishAsync(exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: props,
            body: body);
    }

    private async Task SendMessageToEndpoint(BasicDeliverEventArgs ea, string queue)
    {
        var props = ea.BasicProperties;
        var msgParams = new MessageParameters(props.Type);
        var appEndpoint = AppEndpointHost.Get(msgParams.Name) ??
            throw new ArgumentNullException("msgParams.Name",
                $"Application endpoint {msgParams.Name} not found.");

        var RequestUri = $"{appEndpoint.StringURI}/{msgParams.Exchange}/{msgParams.Operation}/{msgParams.Type}";
        using (HttpClient client = new HttpClient())
        {
            client.Timeout = TimeSpan.FromMinutes(10);
            client.DefaultRequestHeaders.Authorization = appEndpoint.AuthenticationHeader;

            var contentType = new MediaTypeHeaderValue(props.ContentType)
            {
                CharSet = props.ContentEncoding
            };

            var content = new ByteArrayContent(ea.Body.ToArray());
            content.Headers.ContentType = contentType;
            content.Headers.Add("AppId", props.AppId);
            content.Headers.Add("CorrelationId", props.CorrelationId);
            content.Headers.Add("MessageId", props.MessageId);
            content.Headers.Add("ReplyTo", props.ReplyTo);
            if (props.Timestamp.UnixTime > 0)
            {
                content.Headers.Add("Timestamp", props.Timestamp.UnixTime.ToString());
            }
            
            if (props.Headers != null)
            {
                foreach (var header in props.Headers)
                {
                    content.Headers.Add(header.Key, header.Value.ToString());
                    //Encoding.UTF8.GetString((byte[])header.Value));
                }
            }
            
            var cts = new CancellationTokenSource();
            try
            {
                using (HttpResponseMessage response = await client.PostAsync(
                    RequestUri, content, cts.Token))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();
                        await MoveToRetryQueue(ea, queue, errorMsg);
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                // HTTP server is down
                await MoveToRetryQueue(ea, queue, ex.ToString());
            }
            catch (WebException ex)
            {
                // handle web exception
                await MoveToRetryQueue(ea, queue, ex.ToString());
            }
            catch (TaskCanceledException ex)
            {
                if (ex.CancellationToken == cts.Token)
                {
                    // a real cancellation, triggered by the caller
                    await MoveToDeadLetterQueue(ea, ex.ToString());
                }
                else
                {
                    // a web request timeout (possibly other things!?)
                    await MoveToRetryQueue(ea, queue, ex.ToString());
                }
            }
            catch (Exception ex)
            {
                await MoveToInvalidQueue(ea, ex.ToString());
            }
        }
    }

    private BasicProperties CreatePropertiesFromEvent(BasicDeliverEventArgs ea, 
        int attempt = 0, string message = "")
    {
        var props = new BasicProperties();
        props.AppId = ea.BasicProperties.AppId;
        props.ClusterId = ea.BasicProperties.ClusterId;
        props.ContentEncoding = ea.BasicProperties.ContentEncoding;
        props.ContentType = ea.BasicProperties.ContentType;
        props.CorrelationId = ea.BasicProperties.CorrelationId;
        props.DeliveryMode = ea.BasicProperties.DeliveryMode;

        if (ea.BasicProperties.Headers is not null)
        {
            props.Headers = new Dictionary<string, object>(ea.BasicProperties.Headers);
            if (ea.BasicProperties.Headers?.ContainsKey("x-death") ?? false)
            {
                props.Headers.Remove("x-death");
            }
        }
        else
        {
            props.Headers = new Dictionary<string, object>();
        }

        if (attempt != 0)
        {
            var retries = _options.RetryInMilliseconds;
            props.Expiration = retries[attempt - 1];

            props.Headers.Remove("LastErrorMsg");
            props.Headers.Remove("RetryAttempts");

            props.Headers.Add("LastErrorMsg", message);
            props.Headers.Add("RetryAttempts", attempt);
        }
        
        props.MessageId = ea.BasicProperties.MessageId;
        props.Persistent = ea.BasicProperties.Persistent;
        props.Priority = ea.BasicProperties.Priority;
        props.ReplyTo = ea.BasicProperties.ReplyTo;
        props.ReplyToAddress = ea.BasicProperties.ReplyToAddress;
        props.Timestamp = ea.BasicProperties.Timestamp;
        props.Type = ea.BasicProperties.Type;
        props.UserId = ea.BasicProperties.UserId;
        

        return props;
    }

    ~RabbitMQHost()
    {
        Dispose(false);
    }

    public override void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (this.disposed)
        {
            return;
        }

        if (disposing)
        {
            // managed objects here 
        }

        // unmanaged objects here

        foreach (var channel in _channels)
        {
            channel?.Dispose();
        }
        _channels.Clear();

        foreach (var connection in _connections)
        {
            connection?.Dispose();
        }
        _connections.Clear();

        disposed = true;
    }
}
