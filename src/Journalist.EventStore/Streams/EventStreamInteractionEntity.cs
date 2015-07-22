using Journalist.EventStore.Connection;

namespace Journalist.EventStore.Streams
{
    public abstract class EventStreamInteractionEntity : IEventStreamInteractionEntity
    {
        private readonly string m_streamName;
        private readonly IEventStoreConnectionState m_connectionState;

        protected EventStreamInteractionEntity(string streamName, IEventStoreConnectionState connectionState)
        {
            Require.NotEmpty(streamName, "streamName");
            Require.NotNull(connectionState, "connectionState");

            m_streamName = streamName;
            m_connectionState = connectionState;
        }

        public abstract StreamVersion StreamVersion
        {
            get;
        }

        public string StreamName
        {
            get { return m_streamName; }
        }

        public bool IsClosed
        {
            get { return !m_connectionState.IsActive; }
        }

        protected IEventStoreConnectionState ConnectionState
        {
            get { return m_connectionState; }
        }
    }
}