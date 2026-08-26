# KnightBus.PostgreSql Changelog

# 4.1.0
(minor): Nullable reference types are enabled repository-wide, so this package no longer sets
`<Nullable>` itself. `PostgresConfiguration.ConnectionString` is now `string?` rather than
`string = null!`, because `ITransportConfiguration` declares it nullable for the managed-identity
transports and a get/set property's nullability is invariant. `UsePostgres` now throws
`InvalidOperationException` when no connection string was configured, rather than passing null into
`AddNpgsqlDataSource`, and `PostgresSubscriptionChannelReceiver` throws `ArgumentNullException`
for a null subscription.

# 2.3.0
(minor): Allow passing a `Action<NpgsqlDataSourceBuilder>` to `UsePostgres` for custom configuration, for example Azure Managed Identity.

## 1.1.4
(patch): Make `PostgresContants.NpgsqlDataSourceContainerKey` public

## 1.1.3
(patch): Register the `NpgsqlDataSource` using a keyed singleton to avoid multiple registrations for default