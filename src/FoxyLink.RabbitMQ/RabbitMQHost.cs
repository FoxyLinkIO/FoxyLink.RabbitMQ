using System;
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
        private String _consumerTag;
        private IModel _channel;
        private IConnection _connection;

        public RabbitMQHost()
            : this(new RabbitMQHostOptions())
        {
        }

        public RabbitMQHost(RabbitMQHostOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));

            var factory = new ConnectionFactory()
            {
                HostName = _options.HostName,
                UserName = _options.UserName,
                Password = _options.Password 
            };

            factory.AutomaticRecoveryEnabled = true;
            factory.TopologyRecoveryEnabled = true;

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) =>
            {
                Task.Run(async () => await ProcessMessage(ea));
            };

            _consumerTag = _channel.BasicConsume(queue: "1c.foxylink",
                autoAck: false, consumer: consumer);

        }

        private async Task ProcessMessage(Object obj)
        {
            var eventArgs = (BasicDeliverEventArgs)obj;
            var props = eventArgs.BasicProperties;
            var replyProps = _channel.CreateBasicProperties();

            try
            {
                await SendMessageToEndpoint(eventArgs);
            }
            catch(ArgumentException ex)
            {
                MoveToInvalidQueue(props, ex.ToString());
            }
            catch (Exception ex)
            {
                MoveToInvalidQueue(props, ex.ToString());
            }
            finally
            {
                _channel.BasicAck(deliveryTag: eventArgs.DeliveryTag, multiple: false);
            }
        }

        private void MoveToInvalidQueue(IBasicProperties props, string errorMsg)
        {
            var error = new { result = "failed", log = errorMsg };
            var log = JsonConvert.SerializeObject(error);
            var body = Encoding.UTF8.GetBytes(log);

            if (props.ReplyTo != null)
            {
                _channel.BasicPublish(exchange: "",
                    routingKey: props.ReplyTo,
                    basicProperties: props,
                    body: body);
            }

            _channel.BasicPublish(exchange: "",
                                routingKey: _options.InvalidMessageQueue,
                                basicProperties: props,
                                body: body);
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
                content.Headers.Add("Timestamp", props.Timestamp.UnixTime.ToString());

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

            _channel?.Close();
            _connection?.Close();

            disposed = true;

        }
    }
}
