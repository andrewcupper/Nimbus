using System;
using Microsoft.ServiceBus.Messaging;

namespace Nimbus.Infrastructure.MessageSendersAndReceivers
{
    internal class NimbusQueueMessageSender : BatchingMessageSender
    {
        private readonly IQueueManager _queueManager;
        private readonly string _queuePath;
        private readonly ILogger _logger;

        private MessageSender _messageSender;

        public NimbusQueueMessageSender(IQueueManager queueManager, string queuePath, ILogger logger)
            : base(logger)
        {
            _queueManager = queueManager;
            _queuePath = queuePath;
            _logger = logger;
        }

        protected override void SendBatch(BrokeredMessage[] toSend)
        {
            var messageSender = GetMessageSender();

            _logger.Debug("Flushing outbound message queue {0} ({1} messages)", _queuePath, toSend.Length);
            try
            {
                messageSender.SendBatch(toSend);
            }
            catch (Exception exc)
            {
                if (exc.IsTransientFault()) throw;
                DiscardMessageSender();
                throw;
            }
        }

        private MessageSender GetMessageSender()
        {
            if (_messageSender != null) return _messageSender;

            _messageSender = _queueManager.CreateMessageSender(_queuePath).Result;
            return _messageSender;
        }

        private void DiscardMessageSender()
        {
            var messageSender = _messageSender;
            _messageSender = null;

            if (messageSender == null) return;
            if (messageSender.IsClosed) return;

            try
            {
                messageSender.Close();
            }
            catch (Exception exc)
            {
                _logger.Error(exc, "Failed to close MessageSender instance before discarding it.");
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                DiscardMessageSender();
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}