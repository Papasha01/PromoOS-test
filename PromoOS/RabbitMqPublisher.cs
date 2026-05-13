using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace PromoOS
{
    public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
    {
        private readonly RabbitMQ.Client.IConnection _connection;
        private readonly RabbitMQ.Client.IModel _channel;
        private readonly ILogger<RabbitMqPublisher> _logger;

        public RabbitMqPublisher(string connectionString, ILogger<RabbitMqPublisher> logger)
        {
            _logger = logger;
            var factory = new RabbitMQ.Client.ConnectionFactory() { Uri = new Uri(connectionString) };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare("task.events", ExchangeType.Direct, durable: true);
        }

        public void PublishTaskCompleted(TaskItem task)
        {
            try
            {
                var message = new
                {
                    taskId = task.Id,
                    title = task.Title,
                    completedAt = task.CompletedAt,
                    priority = task.Priority.ToString()
                };
                var json = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(json);
                _channel.BasicPublish("task.events", "task.completed", null, body);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish task completed message");
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
        }
    }
}