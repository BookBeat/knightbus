# KnightBus.PostgreSql Changelog

# 2.3.0
(minor): Allow passing a `Action<NpgsqlDataSourceBuilder>` to `UsePostgres` for custom configuration, for example Azure Managed Identity.

## 1.1.4
(patch): Make `PostgresContants.NpgsqlDataSourceContainerKey` public

## 1.1.3
(patch): Register the `NpgsqlDataSource` using a keyed singleton to avoid multiple registrations for default