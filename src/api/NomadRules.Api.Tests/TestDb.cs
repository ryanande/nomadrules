using Microsoft.Extensions.Configuration;
using NomadRules.Api.Infrastructure;

namespace NomadRules.Api.Tests;

public static class TestDb
{
    private const string DefaultConn =
        "Host=localhost;Port=5432;Database=nomadrules_dev;Username=nomadrules;Password=nomadrules";

    public static Db Create()
    {
        var conn = Environment.GetEnvironmentVariable("NOMADRULES_TEST_PG") ?? DefaultConn;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Postgres"] = conn })
            .Build();
        return new Db(config);
    }
}
