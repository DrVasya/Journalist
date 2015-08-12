using Journalist.EventStore.Journal;
using Journalist.EventStore.Streams;
using Journalist.Extensions;

namespace Journalist.EventStore.Connection
{
    public class EventStreamConsumerConfiguration : IEventStreamConsumerConfiguration
    {
        private EventStreamReaderId m_consumerId;
        private string m_consumerName;
        private string m_streamName;
        private bool m_useAutoCommitProcessedStreamPositionBehavior;
        private bool m_startReadingStreamFromEnd;

        public EventStreamConsumerConfiguration()
        {
            m_useAutoCommitProcessedStreamPositionBehavior = true;
            m_consumerName = Constants.DEFAULT_STREAM_READER_NAME;
        }

        public IEventStreamConsumerConfiguration ReadStream(string streamName, bool startReadingFromEnd)
        {
            Require.NotEmpty(streamName, "streamName");

            m_streamName = streamName;
            m_startReadingStreamFromEnd = startReadingFromEnd;

            return this;
        }

        public IEventStreamConsumerConfiguration UseConsumerName(string consumerName)
        {
            Require.NotEmpty(consumerName, "consumerName");

            m_consumerId = null;
            m_consumerName = consumerName;

            return this;
        }

        public IEventStreamConsumerConfiguration UseConsumerId(EventStreamReaderId consumerId)
        {
            Require.NotNull(consumerId, "consumerId");

            m_consumerName = null;
            m_consumerId = consumerId;

            return this;
        }

        public IEventStreamConsumerConfiguration AutoCommitProcessedStreamPosition(bool autoCommit)
        {
            m_useAutoCommitProcessedStreamPositionBehavior = autoCommit;

            return this;
        }

        public void AsserConfigurationCompleted()
        {
            Ensure.True(m_streamName.IsNotNullOrEmpty(), "Stream name was not specified.");
            Ensure.True(m_consumerName.IsNotNullOrEmpty() || m_consumerId != null, "Stream identity was not specified.");
        }

        public string StreamName
        {
            get { return m_streamName; }
        }

        public bool UseAutoCommitProcessedStreamPositionBehavior
        {
            get { return m_useAutoCommitProcessedStreamPositionBehavior; }
        }

        public bool ConsumerRegistrationRequired
        {
            get { return m_consumerId == null; }
        }

        public string ConsumerName
        {
            get
            {
                Ensure.True(m_consumerName.IsNotNullOrEmpty(), "Consumer name was not specified");

                return m_consumerName;
            }
        }

        public EventStreamReaderId ConsumerId
        {
            get
            {
                Ensure.True(m_consumerId != null, "Consumer identifier was not specified");

                return m_consumerId;
            }
        }

        public bool StartReadingStreamFromEnd
        {
            get { return m_startReadingStreamFromEnd; }
        }
    }
}
