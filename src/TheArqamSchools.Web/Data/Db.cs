using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using Dapper;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Opens connections to the SQL Server database. Every query uses Dapper
    /// with parameters (never string concatenation), so input cannot change the SQL.
    /// </summary>
    public static class Db
    {
        /// <summary>
        /// After a connection fails, new connections are refused at once for this long
        /// instead of every request waiting for the connection timeout. Pages that can
        /// work without fresh data (menus, settings, the homepage) then answer quickly
        /// from their last good copy; the rest show the error page straight away.
        /// </summary>
        public static readonly TimeSpan RetryAfter = TimeSpan.FromSeconds(10);

        private static long _blockedUntilTicks;

        private static readonly Lazy<string> ConnectionString = new Lazy<string>(() =>
        {
            var cs = ConfigurationManager.ConnectionStrings["ArqamDb"];
            if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                throw new ConfigurationErrorsException("The 'ArqamDb' connection string is missing from Web.config.");
            return cs.ConnectionString;
        });

        public static IDbConnection Open()
        {
            var until = Interlocked.Read(ref _blockedUntilTicks);
            if (until != 0)
            {
                var now = DateTime.UtcNow.Ticks;
                if (now < until)
                    throw new DatabaseUnavailableException(null);
                // The pause is over: one request tries the database, the others keep
                // answering fast until that attempt succeeds or the next pause ends.
                if (Interlocked.CompareExchange(ref _blockedUntilTicks, now + RetryAfter.Ticks, until) != until)
                    throw new DatabaseUnavailableException(null);
            }
            var conn = new SqlConnection(ConnectionString.Value);
            try
            {
                conn.Open();
            }
            catch (SqlException ex)
            {
                conn.Dispose();
                Interlocked.Exchange(ref _blockedUntilTicks, DateTime.UtcNow.Add(RetryAfter).Ticks);
                throw new DatabaseUnavailableException(ex);
            }
            if (until != 0)
                Interlocked.Exchange(ref _blockedUntilTicks, 0);
            return conn;
        }

        /// <summary>
        /// True when the error means the database could not be reached (connection
        /// failure or the short pause above), did not answer in time (-2), or a pooled
        /// connection broke at the network level (53, 64, 121, 233, 10053, 10054, 10060).
        /// Other SQL errors, such as a missing table or a denied permission, are real
        /// faults and are not hidden by this.
        /// </summary>
        public static bool IsUnavailable(Exception ex)
        {
            if (ex is DatabaseUnavailableException) return true;
            var sql = ex as SqlException;
            if (sql == null) return false;
            switch (sql.Number)
            {
                case -2: case 53: case 64: case 121: case 233: case 10053: case 10054: case 10060:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Column names in the database use snake_case; C# properties use PascalCase.</summary>
        public static void ConfigureDapper()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }
    }

    /// <summary>
    /// The database could not be reached, or new connections are paused after a
    /// connection failure. The original SqlException, if any, is the inner exception.
    /// </summary>
    public class DatabaseUnavailableException : Exception
    {
        public DatabaseUnavailableException(SqlException inner)
            : base(inner == null ? "The database is temporarily unavailable (connections paused after a failure)."
                                 : "The database is temporarily unavailable: " + inner.Message, inner) { }
    }
}
