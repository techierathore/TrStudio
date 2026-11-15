using System.Data;
using System.Globalization;
using Dapper;
using Microsoft.Data.Sqlite;

namespace TrStudio.Engine.Infrastructure.Data;

/// <summary>
/// Dapper and connection set-up for SQLite (phase 3, ADR-016). SQLite has no uuid, timestamp or
/// json types: ids are stored as text, times as UTC ISO-8601 text and JSON as text. These handlers
/// keep every repository's entity types unchanged.
/// </summary>
public static class SqliteDapperSetup
{
    /// <summary>The text format of every stored time: UTC, millisecond precision, matching the scripts' defaults.</summary>
    public const string TimeFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    private static int objRegistered;

    /// <summary>Registers the Guid, DateTime and DateTimeOffset handlers once per process.</summary>
    public static void EnsureRegistered()
    {
        if (Interlocked.Exchange(ref objRegistered, 1) == 1)
        {
            return;
        }

        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.RemoveTypeMap(typeof(Guid?));
        SqlMapper.RemoveTypeMap(typeof(DateTime));
        SqlMapper.RemoveTypeMap(typeof(DateTime?));
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset));
        SqlMapper.RemoveTypeMap(typeof(DateTimeOffset?));
        SqlMapper.AddTypeHandler(new GuidHandler());
        SqlMapper.AddTypeHandler(new DateTimeHandler());
        SqlMapper.AddTypeHandler(new DateTimeOffsetHandler());

        // A money column read back as INTEGER in one row (0 is stored as 0) and REAL in the next
        // (0.31) made Dapper's per-reader mapper throw on the second row; read every stored form.
        SqlMapper.RemoveTypeMap(typeof(decimal));
        SqlMapper.RemoveTypeMap(typeof(decimal?));
        SqlMapper.AddTypeHandler(new DecimalHandler());
    }

    /// <summary>
    /// Per-connection set-up: a <c>now()</c> function returning the UTC time in <see cref="TimeFormat"/>
    /// (the repositories' SQL uses it) and the write-ahead log for concurrent readers.
    /// </summary>
    /// <param name="aConnection">An open connection.</param>
    public static void PrepareConnection(SqliteConnection aConnection)
    {
        aConnection.CreateFunction("now", () => DateTime.UtcNow.ToString(TimeFormat, CultureInfo.InvariantCulture));
        using var vCommand = aConnection.CreateCommand();
        vCommand.CommandText = "PRAGMA journal_mode = WAL; PRAGMA busy_timeout = 10000;";
        vCommand.ExecuteNonQuery();
    }

    /// <summary>Formats a time the way the store keeps it.</summary>
    /// <param name="aValue">The time.</param>
    /// <returns>UTC ISO-8601 text.</returns>
    public static string FormatTime(DateTime aValue)
    {
        var vUtc = aValue.Kind switch
        {
            DateTimeKind.Utc => aValue,
            DateTimeKind.Local => aValue.ToUniversalTime(),
            _ => DateTime.SpecifyKind(aValue, DateTimeKind.Utc)
        };
        return vUtc.ToString(TimeFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>Parses a stored time into a UTC <see cref="DateTime"/>.</summary>
    /// <param name="aValue">The stored value (text, or a number of ticks).</param>
    /// <returns>The UTC time.</returns>
    public static DateTime ParseTime(object aValue) => aValue switch
    {
        DateTime vDate => vDate.Kind == DateTimeKind.Utc ? vDate : DateTime.SpecifyKind(vDate, DateTimeKind.Utc),
        string vText => DateTime.Parse(vText, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        long vTicks => new DateTime(vTicks, DateTimeKind.Utc),
        _ => Convert.ToDateTime(aValue, CultureInfo.InvariantCulture)
    };

    private sealed class GuidHandler : SqlMapper.TypeHandler<Guid>
    {
        public override void SetValue(IDbDataParameter aParameter, Guid aValue)
        {
            aParameter.DbType = DbType.String;
            aParameter.Value = aValue.ToString("D");
        }

        public override Guid Parse(object aValue) => aValue switch
        {
            Guid vGuid => vGuid,
            string vText => Guid.Parse(vText),
            byte[] vBytes => new Guid(vBytes),
            _ => Guid.Parse(aValue.ToString()!)
        };
    }

    /// <summary>
    /// Decimal values: written as before, read from whichever storage class SQLite kept the row in
    /// (INTEGER, REAL or TEXT), so a $0.00 local run and a $0.31 paid run can share one result set.
    /// </summary>
    private sealed class DecimalHandler : SqlMapper.TypeHandler<decimal>
    {
        public override void SetValue(IDbDataParameter aParameter, decimal aValue)
        {
            aParameter.DbType = DbType.Decimal;
            aParameter.Value = aValue;
        }

        public override decimal Parse(object aValue) => aValue switch
        {
            decimal vDecimal => vDecimal,
            long vLong => vLong,
            double vDouble => Convert.ToDecimal(vDouble, CultureInfo.InvariantCulture),
            string vText => decimal.Parse(vText, NumberStyles.Float, CultureInfo.InvariantCulture),
            _ => Convert.ToDecimal(aValue, CultureInfo.InvariantCulture)
        };
    }

    private sealed class DateTimeHandler : SqlMapper.TypeHandler<DateTime>
    {
        public override void SetValue(IDbDataParameter aParameter, DateTime aValue)
        {
            aParameter.DbType = DbType.String;
            aParameter.Value = FormatTime(aValue);
        }

        public override DateTime Parse(object aValue) => ParseTime(aValue);
    }

    private sealed class DateTimeOffsetHandler : SqlMapper.TypeHandler<DateTimeOffset>
    {
        public override void SetValue(IDbDataParameter aParameter, DateTimeOffset aValue)
        {
            aParameter.DbType = DbType.String;
            aParameter.Value = FormatTime(aValue.UtcDateTime);
        }

        public override DateTimeOffset Parse(object aValue) => new(ParseTime(aValue), TimeSpan.Zero);
    }
}
