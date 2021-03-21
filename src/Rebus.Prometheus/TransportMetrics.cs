﻿using System.Threading;
using System.Threading.Tasks;
using Prometheus;
using Rebus.Messages;
using Rebus.Transport;

namespace Rebus.Prometheus
{
    internal class TransportMetrics : ITransport
    {
        private readonly ITransport _decoratee;

        public TransportMetrics(ITransport decoratee)
        {
            _decoratee = decoratee;
        }

        public void CreateQueue(string address)
        {
            _decoratee.CreateQueue(address);
        }

        public Task Send(string destinationAddress, TransportMessage message, ITransactionContext context)
        {
            InstrumentMessage(context, message, Counters.Outgoing);
            return _decoratee.Send(destinationAddress, message, context);
        }

        public async Task<TransportMessage?> Receive(ITransactionContext context, CancellationToken cancellationToken)
        {
            TransportMessage message = await _decoratee.Receive(context, cancellationToken);
            if (message is not null)
            {
                InstrumentMessage(context, message, Counters.Incoming);
            }

            return message;
        }

        public string Address => _decoratee.Address;

        private static void InstrumentMessage(ITransactionContext context, TransportMessage message, IMessageCounters counters)
        {
            counters.Total.Inc();
            counters.InFlight.Inc();
            ITimer messageInTimer = counters.Duration.NewTimer();

            context.OnAborted(_ => counters.Aborted.Inc());
            context.OnDisposed(_ =>
            {
                counters.InFlight.Dec();
                messageInTimer.Dispose();
            });
        }
    }
}
