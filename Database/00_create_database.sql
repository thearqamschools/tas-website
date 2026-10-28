-- =============================================================================
-- THE ARQAM SCHOOLS: CREATE THE DATABASE (SQL Server 2025)
-- Run in SQL Server Management Studio as a sysadmin, then run 01, 02 and 03
-- in order while connected to [TheArqamSchools].
-- =============================================================================
IF DB_ID(N'TheArqamSchools') IS NULL
BEGIN
    -- Case-insensitive, accent-sensitive, supplementary characters (Urdu text is stored as NVARCHAR).
    CREATE DATABASE [TheArqamSchools] COLLATE Latin1_General_100_CI_AS_SC;
END
GO
-- Compatibility level 170 (SQL Server 2025) is required for REGEXP_LIKE in check constraints.
ALTER DATABASE [TheArqamSchools] SET COMPATIBILITY_LEVEL = 170;
ALTER DATABASE [TheArqamSchools] SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
ALTER DATABASE [TheArqamSchools] SET ALLOW_SNAPSHOT_ISOLATION ON;
GO

-- Login used by the website (IIS application pool). Change the password, and
-- store the connection string only in Web.config on the server (never in Git).
USE [master];
IF SUSER_ID(N'arqam_web') IS NULL
    CREATE LOGIN [arqam_web] WITH PASSWORD = N'CHANGE-THIS-to-a-long-random-password', CHECK_POLICY = ON;
GO
USE [TheArqamSchools];
IF USER_ID(N'arqam_web') IS NULL
    CREATE USER [arqam_web] FOR LOGIN [arqam_web];
-- Read and write data only: the website cannot change tables or permissions.
ALTER ROLE [db_datareader] ADD MEMBER [arqam_web];
ALTER ROLE [db_datawriter] ADD MEMBER [arqam_web];
GRANT EXECUTE TO [arqam_web];
GO
