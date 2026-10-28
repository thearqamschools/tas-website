using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using Dapper;

namespace TheArqamSchools.Web.Data
{
    /// <summary>
    /// Opens connections to the SQL Server database. Every query uses Dapper
    /// with parameters (never string concatenation), so input cannot change the SQL.
    /// </summary>
    public static class Db
    {
        private static readonly Lazy<string> ConnectionString = new Lazy<string>(() =>
        {
            var cs = ConfigurationManager.ConnectionStrings["ArqamDb"];
            if (cs == null || string.IsNullOrWhiteSpace(cs.ConnectionString))
                throw new ConfigurationErrorsException("The 'ArqamDb' connection string is missing from Web.config.");
            return cs.ConnectionString;
        });

        public static IDbConnection Open()
        {
            var conn = new SqlConnection(ConnectionString.Value);
            conn.Open();
            return conn;
        }

        /// <summary>Column names in the database use snake_case; C# properties use PascalCase.</summary>
        public static void ConfigureDapper()
        {
            DefaultTypeMap.MatchNamesWithUnderscores = true;
        }
    }
}
