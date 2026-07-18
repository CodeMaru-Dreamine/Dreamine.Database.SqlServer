using Dreamine.Database.Abstractions;
using Dreamine.Database.Core.Mapping;
using Dreamine.Database.Core.Providers;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Dreamine.Database.SqlServer;

/// <summary>
/// \if KO
/// <para>SQL Server용 Dreamine 데이터베이스 공급자 구현을 제공합니다.</para>
/// \endif
/// \if EN
/// <para>Provides a Dreamine database-provider implementation for SQL Server.</para>
/// \endif
/// </summary>
public sealed class SqlServerDatabaseProvider : DatabaseProviderBase
{
    /// <summary>
    /// \if KO
    /// <para>지정한 연결 문자열로 <see cref="SqlServerDatabaseProvider"/>의 새 인스턴스를 초기화합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Initializes a new <see cref="SqlServerDatabaseProvider"/> instance with the specified connection string.</para>
    /// \endif
    /// </summary>
    /// <param name="connectionString">
    /// \if KO
    /// <para>SQL Server 연결 문자열입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL Server connection string.</para>
    /// \endif
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="connectionString"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="connectionString"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="connectionString"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="connectionString"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    public SqlServerDatabaseProvider(string connectionString)
        : base(connectionString)
    {
    }

    /// <summary>
    /// \if KO
    /// <para>SQL Server 공급자 종류를 가져옵니다.</para>
    /// \endif
    /// \if EN
    /// <para>Gets the SQL Server provider kind.</para>
    /// \endif
    /// </summary>
    public override DatabaseProviderKind Kind => DatabaseProviderKind.SqlServer;

    /// <summary>
    /// \if KO
    /// <para>연결 문자열에 지정된 SQL Server 데이터베이스가 없으면 생성합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates the SQL Server database named by the connection string when it does not exist.</para>
    /// \endif
    /// </summary>
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

    /// <summary>
    /// \if KO
    /// <para>연결 문자열에 지정된 SQL Server 데이터베이스가 없으면 비동기적으로 생성합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously creates the SQL Server database named by the connection string when it does not exist.</para>
    /// \endif
    /// </summary>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>작업 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the operation.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>데이터베이스 확인 및 생성 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task representing database verification and creation.</para>
    /// \endif
    /// </returns>
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

    /// <summary>
    /// \if KO
    /// <para>현재 SQL Server 데이터베이스에 지정한 테이블이 존재하는지 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Determines whether the specified table exists in the current SQL Server database.</para>
    /// \endif
    /// </summary>
    /// <param name="tableName">
    /// \if KO
    /// <para>확인할 테이블 이름입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table name to inspect.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부입니다.</para>
    /// \endif
    /// \if EN
    /// <para>Whether the table exists.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is empty or white space.</para>
    /// \endif
    /// </exception>
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

    /// <summary>
    /// \if KO
    /// <para>현재 SQL Server 데이터베이스에 지정한 테이블이 존재하는지 비동기적으로 확인합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Asynchronously determines whether the specified table exists in the current SQL Server database.</para>
    /// \endif
    /// </summary>
    /// <param name="tableName">
    /// \if KO
    /// <para>확인할 테이블 이름입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table name to inspect.</para>
    /// \endif
    /// </param>
    /// <param name="cancellationToken">
    /// \if KO
    /// <para>조회 취소 토큰입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A token used to cancel the query.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>테이블 존재 여부를 결과로 제공하는 작업입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A task whose result indicates whether the table exists.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="tableName"/>이 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="tableName"/> is empty or white space.</para>
    /// \endif
    /// </exception>
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

    /// <summary>
    /// \if KO
    /// <para>구성된 연결 문자열을 사용하는 새 SQL Server 연결을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Creates a new SQL Server connection using the configured connection string.</para>
    /// \endif
    /// </summary>
    /// <returns>
    /// \if KO
    /// <para>닫힌 SQL Server 연결입니다.</para>
    /// \endif
    /// \if EN
    /// <para>A closed SQL Server connection.</para>
    /// \endif
    /// </returns>
    protected override IDbConnection CreateConnection()
    {
        return new SqlConnection(ConnectionString);
    }

    /// <summary>
    /// \if KO
    /// <para>SQL Server 대괄호 문법으로 식별자를 안전하게 인용합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Safely quotes an identifier using SQL Server bracket syntax.</para>
    /// \endif
    /// </summary>
    /// <param name="identifier">
    /// \if KO
    /// <para>인용할 식별자입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The identifier to quote.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>이스케이프하고 인용한 식별자입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The escaped and quoted identifier.</para>
    /// \endif
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// \if KO
    /// <para><paramref name="identifier"/>가 <see langword="null"/>인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="identifier"/> is <see langword="null"/>.</para>
    /// \endif
    /// </exception>
    /// <exception cref="ArgumentException">
    /// \if KO
    /// <para><paramref name="identifier"/>가 비어 있거나 공백인 경우 발생합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Thrown when <paramref name="identifier"/> is empty or white space.</para>
    /// \endif
    /// </exception>
    protected override string QuoteIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);
        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    /// <summary>
    /// \if KO
    /// <para>IDENTITY 키를 지원하는 SQL Server CREATE TABLE SQL을 만듭니다.</para>
    /// \endif
    /// \if EN
    /// <para>Builds SQL Server CREATE TABLE SQL with identity-key support.</para>
    /// \endif
    /// </summary>
    /// <param name="map">
    /// \if KO
    /// <para>테이블 엔터티 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The table entity map.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>조건부 SQL Server CREATE TABLE SQL입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The conditional SQL Server CREATE TABLE SQL.</para>
    /// \endif
    /// </returns>
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

    /// <summary>
    /// \if KO
    /// <para>CLR 속성 형식을 대응하는 SQL Server 열 형식으로 변환합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Converts a CLR property type to its corresponding SQL Server column type.</para>
    /// \endif
    /// </summary>
    /// <param name="property">
    /// \if KO
    /// <para>변환할 속성 매핑입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The property mapping to convert.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>SQL Server 열 형식 선언입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The SQL Server column-type declaration.</para>
    /// \endif
    /// </returns>
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

    /// <summary>
    /// \if KO
    /// <para>SQL 문자열 리터럴에 포함될 작은따옴표를 이스케이프합니다.</para>
    /// \endif
    /// \if EN
    /// <para>Escapes apostrophes for inclusion in a SQL string literal.</para>
    /// \endif
    /// </summary>
    /// <param name="value">
    /// \if KO
    /// <para>이스케이프할 값입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The value to escape.</para>
    /// \endif
    /// </param>
    /// <returns>
    /// \if KO
    /// <para>작은따옴표가 이스케이프된 값입니다.</para>
    /// \endif
    /// \if EN
    /// <para>The value with apostrophes escaped.</para>
    /// \endif
    /// </returns>
    private static string EscapeSqlLiteral(string value)
    {
        return value.Replace("'", "''", StringComparison.Ordinal);
    }
}
