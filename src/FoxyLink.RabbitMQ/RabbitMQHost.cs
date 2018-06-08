using System;
using System.Collections.Generic;
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

            var factory = new ConnectionFactory()
            {
                AutomaticRecoveryEnabled = true,
                DispatchConsumersAsync = true,
                TopologyRecoveryEnabled = true,
                Uri = new Uri(_options.AmqpUri)
            };
  
            for (var i = 0; i < _options.NodesCount; i++)
            {
                var connection = factory.CreateConnection();
                _connections.Add(connection);

                var channel = connection.CreateModel();
                _channels.Add(channel);

                channel.BasicQos(0, _options.PrefetchCount, false);
                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.Received += async (o, ea) =>
                {
                    try
                    {
                        await SendMessageToEndpoint(ea);
                        channel.BasicAck(deliveryTag: ea.DeliveryTag,
                            multiple: false);
                    }
                    catch (Exception ex)
                    {
                        MoveToInvalidQueue(ea.BasicProperties, ex.ToString());
                    }
                };
                channel.BasicConsume(consumer, _options.MessageQueue,
                    autoAck: false);
            }
        }

        private void SetUpQueues()
        {
            var factory = new ConnectionFactory();
            factory.Uri = new Uri(_options.AmqpUri);
            using (var connection = factory.CreateConnection())
            {
                using (var model = connection.CreateModel())
                {
                    model.QueueDeclare(_options.MessageQueue, true, false, false, null);
                    model.QueueDeclare(_options.InvalidMessageQueue, true, false, false, null);
                }
            }
        }

        private void MoveToInvalidQueue(IBasicProperties props, string errorMsg)
        {
            var error = new { result = "failed", log = errorMsg };
            var log = JsonConvert.SerializeObject(error);
            var body = Encoding.UTF8.GetBytes(log);

            var factory = new ConnectionFactory();
            factory.Uri = new Uri(_options.AmqpUri);
            using (var connection = factory.CreateConnection())
            {
                using (var model = connection.CreateModel())
                {
                    var eProps = model.CreateBasicProperties();
                    eProps.AppId = props.AppId;
                    eProps.ContentEncoding = props.ContentEncoding;
                    eProps.ContentType = props.ContentType;

                    if (!String.IsNullOrWhiteSpace(props.CorrelationId))
                    {
                        eProps.CorrelationId = props.CorrelationId;
                    }

                    eProps.CorrelationId = props.CorrelationId;
                    eProps.DeliveryMode = props.DeliveryMode;

                    if (!String.IsNullOrWhiteSpace(props.MessageId))
                    {
                        eProps.MessageId = props.MessageId;
                    }
                   
                    eProps.ReplyTo = props.ReplyTo;
                    eProps.Timestamp = props.Timestamp;

                    if (!String.IsNullOrWhiteSpace(props.Type))
                    {
                        eProps.Type = props.Type;
                    }

                    if (props.ReplyTo != null)
                    {
                        model.BasicPublish(exchange: "",
                            routingKey: props.ReplyTo,
                            basicProperties: eProps,
                            body: body);
                    }
                    else
                    {
                        model.BasicPublish(exchange: "",
                            routingKey: _options.InvalidMessageQueue,
                            basicProperties: props,
                            body: body);
                    }
                }
            }
        }

        private async Task SendMessageToEndpoint(BasicDeliverEventArgs eventArgs)
        {
            var props = eventArgs.BasicProperties;
            var msgParams = new MessageParameters(props.Type);
            var appEndpoint = AppEndpointHost.Get(msgParams.Name) ??
                throw new ArgumentNullException("msgParams.Name",
                    $"Application endpoint {msgParams.Name} not found.");

            var RequestUri = $"{appEndpoint.StringURI}/{msgParams.Exchange}/{msgParams.Operation}/{msgParams.Type}";
            using (HttpClient client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromMinutes(10);
                client.DefaultRequestHeaders.Authorization = appEndpoint.AuthenticationHeader;

                var contentType = new MediaTypeHeaderValue(props.ContentType);
                contentType.CharSet = props.ContentEncoding;

                var content = new ByteArrayContent(eventArgs.Body);
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
                        content.Headers.Add(header.Key, Encoding.UTF8.GetString((byte[])header.Value));
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
                            MoveToInvalidQueue(props, errorMsg);
                            //TryAgainLater(props.ReplyTo, replyProps, errorMsg);
                        }
                    }
                }
                catch (WebException ex)
                {
                    // handle web exception
                    //TryAgainLater(props.ReplyTo, replyProps, ex.ToString());
                    MoveToInvalidQueue(props, ex.ToString());
                }
                catch (TaskCanceledException ex)
                {
                    if (ex.CancellationToken == cts.Token)
                    {
                        // a real cancellation, triggered by the caller
                        //TryAgainLater(props.ReplyTo, replyProps, ex.ToString());
                        MoveToInvalidQueue(props, ex.ToString());
                    }
                    else
                    {
                        // a web request timeout (possibly other things!?)
                        //TryAgainLater(props.ReplyTo, replyProps, ex.ToString());
                        MoveToInvalidQueue(props, ex.ToString());
                    }
                }
                catch (Exception ex)
                {
                    //TryAgainLater(props.ReplyTo, replyProps, ex.ToString());
                    MoveToInvalidQueue(props, ex.ToString());
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
