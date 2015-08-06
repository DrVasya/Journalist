﻿using System;
using Journalist.EventStore.Notifications.Types;
using Journalist.EventStore.Streams;
using Journalist.EventStore.UnitTests.Infrastructure.TestData;
using Xunit;

namespace Journalist.EventStore.UnitTests.Notifications.Types
{
    public class EventStreamUpdatedTests
    {
        [Theory, AutoMoqData]
        public void IsAddressedTo_WhenNotificationWasNotAddressed_Throws(EventStreamUpdated notification, EventStreamConsumerId consumerId)
        {
            Assert.Throws<InvalidOperationException>(() => notification.IsAddressedTo(consumerId));
        }

        [Theory, AutoMoqData]
        public void IsAddressedTo_WhenRecipientSpecifiedDirectly_ReturnsTrue(EventStreamUpdated notification, EventStreamConsumerId consumerId)
        {
            Assert.True(notification
                .SendTo(consumerId)
                .IsAddressedTo(consumerId));
        }

        [Theory, AutoMoqData]
        public void AddressedNotification_WhenRecipientIsDifferent_ReturnsFalse(
            EventStreamUpdated notification,
            EventStreamConsumerId originalConsumer,
            EventStreamConsumerId sendedConsumer)
        {
            Assert.False(notification
                .SendTo(originalConsumer)
                .IsAddressedTo(sendedConsumer));
        }

        [Theory, AutoMoqData]
        public void SendTo_PreservesNotificationPropertiesValues(EventStreamUpdated notification, EventStreamConsumerId consumerId)
        {
            var addressedNotification = (EventStreamUpdated)notification.SendTo(consumerId);

            Assert.Equal(notification.StreamName, addressedNotification.StreamName);
            Assert.Equal(notification.FromVersion, addressedNotification.FromVersion);
            Assert.Equal(notification.ToVersion, addressedNotification.ToVersion);
            Assert.Equal(notification.NotificationId, addressedNotification.NotificationId);
            Assert.Equal(notification.NotificationType, addressedNotification.NotificationType);
        }

    }
}