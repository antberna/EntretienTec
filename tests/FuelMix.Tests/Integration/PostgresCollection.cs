namespace FuelMix.Tests.Integration;

[CollectionDefinition("Postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresTestDatabase>;
