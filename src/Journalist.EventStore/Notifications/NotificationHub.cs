using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Journalist.Collections;
using Journalist.EventStore.Connection;
using Journalist.EventStore.Journal;
using Journalist.EventStore.Notifications.Channels;
using Journalist.EventStore.Notifications.Listeners;
using Journalist.EventStore.Notifications.Timeouts;
using Journalist.EventStore.Streams;
using Journalist.Extensions;
using Serilog;

namespace Journalist.EventStore.Notifications
{
    public class NotificationHub : INotificationHub
    {
        private static readonly ILogger s_logger = Log.ForContext<NotificationHub>();

        private readonly Dictionary<EventStreamReaderId, NotificationListenerSubscription> m_subscriptions = new Dictionary<EventStreamReaderId, NotificationListenerSubscription>();
        private readonly Dictionary<INotificationListener, EventStreamReaderId> m_listenerSubscriptions = new Dictionary<INotificationListener, EventStreamReaderId>();
        private readonly INotificationsChannel m_channel;
        private readonly IEventStreamConsumersRegistry m_consumersRegistry;
        private readonly IPollingTimeout m_timeout;

        private CancellationTokenSource m_pollingCancellationToken;
        private Task m_processingTask;
        private int m_processingCount;
        private int m_maxProcessingCount;

        public NotificationHub(
            INotificationsChannel channel,
            IEventStreamConsumersRegistry consumersRegistry,
            IPollingTimeout timeout)
        {
            Require.NotNull(channel, "channel");
            Require.NotNull(consumersRegistry, "consumersRegistry");
            Require.NotNull(timeout, "timeout");

            m_channel = channel;
            m_consumersRegistry = consumersRegistry;
            m_timeout = timeout;
        }

        public Task NotifyAsync(INotification notification)
        {
            Require.NotNull(notification, "notification");

            return m_channel.SendAsync(notification);
        }

        public void Subscribe(INotificationListener listener)
        {
            Require.NotNull(listener, "listener");

            var consumerId = RegisterEventListenerConsumer(listener);
            m_subscriptions.Add(consumerId, new NotificationListenerSubscription(consumerId, m_channel, listener));
            m_listenerSubscriptions.Add(listener, consumerId);
        }

        public void Unsubscribe(INotificationListener listener)
        {
            Require.NotNull(listener, "listener");

            var subscriptionId = m_listenerSubscriptions[listener];
            m_subscriptions.Remove(subscriptionId);
        }

        public void StartNotificationProcessing(IEventStoreConnection connection)
        {
            Require.NotNull(connection, "connection");

            if (m_subscriptions.Any())
            {
                m_maxProcessingCount = Constants.Settings.DEFAULT_MAX_NOTIFICATION_PROCESSING_COUNT * m_subscriptions.Count;

                foreach (var subscriptions in m_subscriptions.Values)
                {
                    subscriptions.Start(connection);
                }

                m_pollingCancellationToken = new CancellationTokenSource();
                m_processingTask = ProcessNotificationFromChannel(m_pollingCancellationToken.Token);
            }
        }

        public void StopNotificationProcessing()
        {
            if (m_subscriptions.Any())
            {
                // Call sequence is important. First we stop receiving
                // of a new notifications then wait for completion of
                // received notifications processing.
                //
                if (m_pollingCancellationToken != null)
                {
                    m_pollingCancellationToken.Cancel();
                    m_processingTask.Wait();
                }

                // Absense of new a notifications in the subscription
                // stop phase is essential guarantee.
                //
                foreach (var subscription in m_subscriptions.Values)
                {
                    subscription.Stop();
                }
            }
        }

        private EventStreamReaderId RegisterEventListenerConsumer(INotificationListener listener)
        {
            return Task.Run(() =>
                m_consumersRegistry.RegisterAsync(listener.GetType().FullName)).Result;
        }

        private async Task ProcessNotificationFromChannel(CancellationToken token)
        {
            // switch to the background task
            await Task.Yield();

            s_logger.Information("Starting notification processing cycle...");

            while (!token.IsCancellationRequested)
            {
                var notifications = await ReceiveNotificationsAsync();

                if (notifications.IsEmpty())
                {
                    s_logger.Debug("No notifications for processing. Request channel after: {Timeout}.", m_timeout);

                    await m_timeout.WaitAsync(token);
                    m_timeout.Increase();
                }
                else
                {
                    m_timeout.Reset();

                    foreach (var notification in notifications)
                    {
                        ProcessNotification(notification);
                    }
                }
            }

            s_logger.Information("Notification processing cycle was stopped.");
        }

        private bool RequestNotificationsRequired()
        {
            var observedProcessingCount = m_processingCount;
            if (observedProcessingCount >= m_maxProcessingCount)
            {
                s_logger.Debug(
                    "Number of notification processing ({ProcessingCount}) exceeded maximum value ({MaxProcessingCount}).",
                    observedProcessingCount,
                    m_maxProcessingCount);

                return false;
            }

            return true;
        }

        private async Task<INotification[]> ReceiveNotificationsAsync()
        {
            var notifications = EmptyArray.Get<INotification>();
            if (RequestNotificationsRequired())
            {
                notifications = await m_channel.ReceiveNotificationsAsync();

                s_logger.Debug(
                    "Receive {NotificationCount} notifications {NotificationIds}.",
                    notifications.Length,
                    notifications.Select(n => n.NotificationId));
            }

            return notifications;
        }

#pragma warning disable 4014
        private void ProcessNotification(INotification notification)
        {
            foreach (var subscription in m_subscriptions.Values)
            {
                Interlocked.Increment(ref m_processingCount);

                subscription
                    .HandleNotificationAsync(notification)
                    .ContinueWith(handlingTask =>
                    {
                        if (handlingTask.Exception != null)
                        {
                            s_logger.Fatal(
                                handlingTask.Exception.GetBaseException(),
                                "UNHANDLED EXCEPTION in NotificationListenerSubscription.");
                        }

                        Interlocked.Decrement(ref m_processingCount);
                    });
            }
        }
#pragma warning restore 4014
    }
}
