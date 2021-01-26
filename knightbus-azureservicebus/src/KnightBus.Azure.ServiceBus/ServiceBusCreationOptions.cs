namespace KnightBus.Azure.ServiceBus
{
    public class ServiceBusCreationOptions
    {
        /// <summary>
        /// Defines whether ordering needs to be maintained. If true, messages sent to topic will be 
        /// forwarded to the subscription in order. 
        /// </summary>
        /// <remarks>Defaults to false.</remarks>
        public bool SupportOrdering { get; set; } = false;

        /// <summary>
        /// Indicates whether server-side batched operations are enabled.
        /// </summary>
        /// <remarks>Defaults to true.</remarks>
        public bool EnableBatchedOperations { get; set; } = true;
    }
}