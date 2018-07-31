using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FoxyLink.RabbitMQ
{
    public class RabbitMQHost : QueueHost
    {
        private readonly RabbitMQHostOptions _options;
        private const string _invalidQueue = @"foxylink.invalid";
        private const string _deadLetterQueue = @"foxylink.dead.letter";
        private const string _deadLetterExchange = @"foxylink.dead.letter";

        private bool disposed = false;
        private List<IModel> _channels = new List<IModel>();
        private List<IConnection> _connections = new List<IConnection>();

        public RabbitMQHost()
            : this(new RabbitMQHostOptions())
        {
        }

        public RabbitMQHost(RabbitMQHostOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            SetUpQueues();

            var factory = ConnectionFactory();
            foreach (var queue in _options.Queues)
            {
                for (var i = 0; i < queue.NodesCount; i++)
                {
                    var connection = factory.CreateConnection();
                    _connections.Add(connection);

                    var channel = connection.CreateModel();
                    _channels.Add(channel);

                    channel.BasicQos(0, queue.PrefetchCount, false);
                    var consumer = new AsyncEventingBasicConsumer(channel);
                    consumer.Received += async (o, ea) =>
                    {
                        try
                        {
                            await SendMessageToEndpoint(ea, queue.Name);
                        }
                        catch (Exception ex)
                        {
                            MoveToInvalidQueue(ea, ex.ToString());
                        }
                        finally
                        {
                            channel.BasicAck(deliveryTag: ea.DeliveryTag,
                                multiple: false);
                        }
                    };
                    channel.BasicConsume(consumer, queue.Name, autoAck: false);
                }
            }
        }

        private ConnectionFactory ConnectionFactory()
        {
            return new ConnectionFactory()
            {
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true,
                TopologyRecoveryEnabled = true,
                Uri = new Uri(_options.AmqpUri)
            };
        }

        private void SetUpQueues()
        {
            var factory = ConnectionFactory();
            using (var connection = factory.CreateConnection())
            using (var model = connection.CreateModel())
            {
                model.QueueDeclare(_invalidQueue, true, false, false, null);

                model.ExchangeDeclare(_deadLetterExchange, @"direct", true, false);
                model.QueueDeclare(_deadLetterQueue, true, false, false, null);
                model.QueueBind(_deadLetterQueue, _deadLetterExchange, _deadLetterQueue);

                foreach (var queue in _options.Queues)
                {
                    model.QueueDeclare(queue.Name, true, false, false, 
                        new Dictionary<string, object>
                        {
                            { "x-dead-letter-exchange", _deadLetterExchange },
                            { "x-dead-letter-routing-key", _deadLetterQueue}
                        });

                    var retries = _options.RetryInMilliseconds;
                    if (_options.RetryInMilliseconds.Count > 0)
                    {
                        var retryQueue = $"{queue.Name}.retry";
                        model.QueueDeclare(retryQueue, true, false, false,
                            new Dictionary<string, object>
                            {
                                { "x-dead-letter-exchange", _deadLetterExchange },
                                { "x-dead-letter-routing-key", queue.Name}
                            });
                        model.QueueBind(queue.Name, _deadLetterExchange, queue.Name);
                    }
                }
            }
        }

        private void MoveToRetryQueue(BasicDeliverEventArgs ea, string queue, string message)
        {
            var attempt = 1;
            var retries = _options.RetryInMilliseconds;
            if (retries.Count == 0)
            {
                MoveToDeadLetterQueue(ea, $"The number of retry attempts isn't set. {message}");
                return;
            }

            var props = ea.BasicProperties;
            if (props.Headers.ContainsKey("x-death"))
            {
                attempt = (from list in props.Headers["x-death"] as List<object>
                        from dict in list as Dictionary<string, object>
                        where dict.Key == "count"
                        select Convert.ToInt32(dict.Value)).Sum() + 1;
            }

            if (attempt > retries.Count)
            {
                MoveToDeadLetterQueue(ea, $"Message has exceeded the maximum number of retry attempts. {message}");
                return;
            }

            var factory = ConnectionFactory();
            using (var connection = factory.CreateConnection())
            using (var model = connection.CreateModel())
            {
                props.Expiration = retries[attempt-1];
                model.BasicPublish(exchange: "",
                    routingKey: $"{queue}.retry",
                    basicProperties: props,
                    body: ea.Body);
            }
        }

        private void MoveToDeadLetterQueue(BasicDeliverEventArgs ea, string message)
        {
            var props = ea.BasicProperties;
            props.Headers.Add("msg-dead-letter", JsonConvert.SerializeObject(
                new { success = false, log = message }));

            MoveToServiceQueue(_deadLetterExchange, _deadLetterQueue, props, ea.Body);
        }

        private void MoveToInvalidQueue(BasicDeliverEventArgs ea, string message)
        {
            var props = ea.BasicProperties;
            props.Headers.Add("msg-invalid-error", JsonConvert.SerializeObject(
                new { success = false, log = message }));

            MoveToServiceQueue("", _invalidQueue, props, ea.Body);
        }

        private void MoveToServiceQueue(string exchange, string routingKey, IBasicProperties props, byte[] body)
        {
            var factory = ConnectionFactory();
            using (var connection = factory.CreateConnection())
            using (var model = connection.CreateModel())
            {
                if (props.ReplyTo != null)
                {
                    model.BasicPublish(exchange: "",
                        routingKey: props.ReplyTo,
                        basicProperties: props,
                        body: body);
                }

                model.BasicPublish(exchange: exchange,
                    routingKey: routingKey,
                    basicProperties: props,
                    body: body);
            }
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

                var content = new ByteArrayContent(ea.Body);
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
                            MoveToRetryQueue(ea, queue, errorMsg);
                        }
                    }
                }
                catch (WebException ex)
                {
                    // handle web exception
                    MoveToRetryQueue(ea, queue, ex.ToString());
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.CancellationToken == cts.Token)
                    {
                        // a real cancellation, triggered by the caller
                        MoveToDeadLetterQueue(ea, ex.ToString());
                    }
                    else
                    {
                        // a web request timeout (possibly other things!?)
                        MoveToRetryQueue(ea, queue, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    MoveToInvalidQueue(ea, ex.ToString());
                }
            }
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
}
