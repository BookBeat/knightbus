# KnightBus.PostgreSql Changelog

## 4.1.0
(minor): Implement `IMessageLockHandler<T>` on `PostgresMessageStateHandler` so `ExtendMessageLockDurationMiddleware` can renew message locks. The fetch lock is passed as a `TimeSpan`, so fractional durations no longer truncate to whole seconds.

## 2.3.0
(minor): Allow passing a `Action<NpgsqlDataSourceBuilder>` to `UsePostgres` for custom configuration, for example Azure Managed Identity.

## 1.1.4
(patch): Make `PostgresContants.NpgsqlDataSourceContainerKey` public

## 1.1.3
(patch): Register the `NpgsqlDataSource` using a keyed singleton to avoid multiple registrations for default