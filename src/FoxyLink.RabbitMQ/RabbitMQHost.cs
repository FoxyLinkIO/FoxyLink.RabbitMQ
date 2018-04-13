using System;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FoxyLink.RabbitMQ
{
    public class RabbitMQHost : QueueHost
    {
        private bool disposed = false;
        private String _consumerTag;
        private IModel _channel;
        private IConnection _connection;

        private readonly RabbitMQHostOptions _options;

        public RabbitMQHost()
            : this(new RabbitMQHostOptions())
        {
        }

        public RabbitMQHost(RabbitMQHostOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            _options = options;

            var factory = new ConnectionFactory()
            {
                HostName = Configuration.Current["AccessData:RabbitMQ:HostName"],
                UserName = Configuration.Current["AccessData:RabbitMQ:UserName"],
                Password = Configuration.Current["AccessData:RabbitMQ:Password"]
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
            var ea = (BasicDeliverEventArgs)obj;
            var props = ea.BasicProperties;
            var replyProps = _channel.CreateBasicProperties();

            _channel.BasicPublish(exchange: "",
                                routingKey: "test",
                                basicProperties: replyProps,
                                body: ea.Body);

            _channel.BasicAck(deliveryTag: ea.DeliveryTag, multiple: false);

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
