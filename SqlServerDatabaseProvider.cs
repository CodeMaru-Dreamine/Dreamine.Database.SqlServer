using Dreamine.Database.Abstractions;
using Dreamine.Database.Core.Mapping;
using Dreamine.Database.Core.Providers;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Dreamine.Database.SqlServer;

public sealed class SqlServerDatabaseProvider : DatabaseProviderBase
{
    public SqlServerDatabaseProvider(string connectionString)
        : base(connectionString)
    {
    }

    public override DatabaseProviderKind Kind => DatabaseProviderKind.SqlServer;

    public override void EnsureDatabaseExists()
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            base.EnsureDatabaseExists();
            return;
        }

        builder.InitialCatalog = "master";

        using var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{EscapeSqlLiteral(databaseName)}') IS NULL
            CREATE DATABASE {QuoteIdentifier(databaseName)}
            """;
        command.ExecuteNonQuery();
    }

    public override async Task EnsureDatabaseExistsAsync(CancellationToken cancellationToken = default)
    {
        var builder = new SqlConnectionStringBuilder(ConnectionString);
        var databaseName = builder.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName))
        {
            await base.EnsureDatabaseExistsAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        builder.InitialCatalog = "master";

        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            IF DB_ID(N'{EscapeSqlLiteral(databaseName)}') IS NULL
            CREATE DATABASE {QuoteIdentifier(databaseName)}
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public override bool IsTableExists(string tableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        const string sql = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @TableName
            """;

        return ExecuteScalar<int>(sql, new { TableName = tableName }) > 0;
    }

    public override async Task<bool> IsTableExistsAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

        const string sql = """
            SELECT COUNT(1)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_NAME = @TableName
            """;

        var count = await ExecuteScalarAsync<int>(sql, new { TableName = tableName }, cancellationToken)
            .ConfigureAwait(false);
        return count > 0;
    }

    protected override IDbConnection CreateConnection()
    {
        return new SqlConnection(ConnectionString);
    }

    protected override string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    protected override string BuildCreateTableSql(DatabaseEntityMap map)
    {
        var columns = map.Properties.Select(property =>
        {
            var sql = $"{QuoteIdentifier(property.ColumnName)} {GetSqlType(property)}";
            if (property.IsKey)
            {
                sql += property.IsGenerated ? " IDENTITY(1,1) PRIMARY KEY" : " PRIMARY KEY";
            }

            return sql;
        });

        return $"""
            IF OBJECT_ID(N'{map.TableName}', N'U') IS NULL
            CREATE TABLE {QuoteIdentifier(map.TableName)} ({string.Join(", ", columns)})
            """;
    }

    protected override string GetSqlType(DatabasePropertyMap property)
    {
        var type = property.PropertyType;

        if (type == typeof(bool))
        {
            return "BIT";
        }

        if (type == typeof(byte) || type == typeof(short))
        {
            return "SMALLINT";
        }

        if (type == typeof(int))
        {
            return "INT";
        }

        if (type == typeof(long))
        {
            return "BIGINT";
        }

        if (type == typeof(float))
        {
            return "REAL";
        }

        if (type == typeof(double))
        {
            return "FLOAT";
        }

        if (type == typeof(decimal))
        {
            return "DECIMAL(18, 4)";
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
        {
            return "DATETIME2";
        }

        if (type == typeof(byte[]))
        {
            return "VARBINARY(MAX)";
        }

        return "NVARCHAR(MAX)";
    }

    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
