﻿using MediatR;
using MicroRabbit.Domain.Core.Bus;
using MicroRabbit.Domain.Core.Commands;
using MicroRabbit.Domain.Core.Events;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace MicroRabbit.Infra.Bus
{
    public sealed class RabbitMQBus : IEventBus
    {
        private readonly IMediator _mediatr;
        private readonly Dictionary<string, List<Type>> _handlers;
        private readonly List<Type> _eventTypes;

        public RabbitMQBus(IMediator mediatr)
        {
            _mediatr = mediatr;
            _handlers = new Dictionary<string, List<Type>>();
            _eventTypes = new List<Type>();
        }

        public void Publish<T>(T @event) where T : Event
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // use reflection to get the event type name
            var eventName = @event.GetType().Name;
            channel.QueueDeclare(queue: eventName, durable: false, exclusive: false, autoDelete: false, arguments: null);
            var mesage = JsonSerializer.Serialize(@event);
            var body = Encoding.UTF8.GetBytes(mesage);
            channel.BasicPublish(exchange: "", routingKey: eventName, basicProperties: null, body: body);
        }



        public Task SendCommand<T>(T command) where T : Command
        {
            return _mediatr.Send(command);
        }

        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler
        {
            var eventName = typeof(T).Name;
            var handlerType = typeof(TH);

            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }

            if (!_handlers.ContainsKey(eventName))
            {
                _handlers.Add(eventName, new List<Type>());
            }

            if (_handlers.ContainsKey(eventName))
            {
                if (_handlers[eventName].Any(s => s.GetType() == handlerType))
                    throw new ArgumentException($"Handler Type {handlerType.Name} is already registered for {eventName}", nameof(handlerType));

                _handlers[eventName].Add(handlerType);

                StartBasicConsume<T>();
            }
        }

        private void StartBasicConsume<T>() where T : Event
        {
            var factory = new ConnectionFactory { HostName = "localhost", DispatchConsumersAsync = true };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();

            var eventName = typeof(T).Name;

            channel.QueueDeclare(queue: eventName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.Received += Consumer_Received;
            channel.BasicConsume(queue: eventName, autoAck: true, consumer: consumer);
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            var eventName = e.RoutingKey;
            var message = Encoding.UTF8.GetString(e.Body.ToArray());

            try
            {
                await ProcessEvent(eventName, message).ConfigureAwait(false);
            }
            catch
            {

            }
        }

        private async Task ProcessEvent(string eventName, string message)
        {
            if (_handlers.ContainsKey(eventName))
            {
                var subscriptions = _handlers[eventName];
                foreach (var subscription in subscriptions)
                {
                    var handler = Activator.CreateInstance(subscription);
                    if (handler != null)
                    {
                        var eventType = _eventTypes.SingleOrDefault(t => t.Name == eventName);
                        var @event = JsonSerializer.Deserialize(json: message, returnType: eventType);
                        var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { @event });
                    }
                }
            }
        }
    }
}
