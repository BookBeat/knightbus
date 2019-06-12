namespace KnightBus.Core.Sagas
{
    public static class SagaExtensions
    {
        public static IHostConfiguration EnableSagas(this IHostConfiguration configuration, ISagaStore store)
        {
            configuration.Middlewares.Add(new SagaMiddleware(store));
            return configuration;
        }
    }
}