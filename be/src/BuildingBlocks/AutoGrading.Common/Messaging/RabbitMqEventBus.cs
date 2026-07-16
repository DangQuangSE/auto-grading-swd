using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using AutoGrading.Contracts.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace AutoGrading.Common.Messaging;

/// <summary>
/// Topic-exchange event bus. Routing key = event type name; each subscribing service
/// gets its own durable queue ("{ServiceName}.{EventName}") bound to that routing key,
/// so every service receives its own copy of a published event (pub/sub fan-out).
/// </summary>
public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ConcurrentDictionary<string, Type> _eventTypesByName = new();
    private readonly ConcurrentDictionary<string, List<Type>> _handlersByEventName = new();

    public RabbitMqEventBus(IOptions<RabbitMqOptions> options, IServiceScopeFactory scopeFactory)
    {
        _options = options.Value;
        _scopeFactory = scopeFactory;

        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            DispatchConsumersAsync = true,
        };

        var retries = 5;
        while (true)
        {
            try
            {
                _connection = factory.CreateConnection();
                break;
            }
            catch (RabbitMQ.Client.Exceptions.BrokerUnreachableException) when (retries > 0)
            {
                retries--;
                Thread.Sleep(3000);
            }
        }

        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationEvent
    {
        var routingKey = typeof(TEvent).Name;
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(@event, @event.GetType()));

        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";

        _channel.BasicPublish(_options.Exchange, routingKey, properties, body);

        return Task.CompletedTask;
    }

    public void Subscribe<TEvent, THandler>()
        where TEvent : IntegrationEvent
        where THandler : IIntegrationEventHandler<TEvent>
    {
        var eventName = typeof(TEvent).Name;
        var queueName = $"{_options.ServiceName}.{eventName}";

        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queueName, _options.Exchange, eventName);

        _eventTypesByName[eventName] = typeof(TEvent);
        var handlers = _handlersByEventName.GetOrAdd(eventName, _ => new List<Type>());
        if (!handlers.Contains(typeof(THandler)))
        {
            handlers.Add(typeof(THandler));
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, ea) =>
        {
            var eventName2 = ea.RoutingKey;
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());

            if (_eventTypesByName.TryGetValue(eventName2, out var eventType) &&
                _handlersByEventName.TryGetValue(eventName2, out var handlerTypes))
            {
                var @event = JsonSerializer.Deserialize(json, eventType);
                if (@event is not null)
                {
                    using var scope = _scopeFactory.CreateScope();
                    foreach (var handlerType in handlerTypes)
                    {
                        if (scope.ServiceProvider.GetService(handlerType) is not { } handler)
                        {
                            continue;
                        }

                        var handleMethod = handlerType.GetMethod("HandleAsync");
                        if (handleMethod is not null)
                        {
                            await (Task)handleMethod.Invoke(handler, new[] { @event, CancellationToken.None })!;
                        }
                    }
                }
            }

            _channel.BasicAck(ea.DeliveryTag, multiple: false);
        };

        _channel.BasicConsume(queueName, autoAck: false, consumer);
    }

    public void Dispose()
    {
        _channel.Dispose();
        _connection.Dispose();
    }
}
