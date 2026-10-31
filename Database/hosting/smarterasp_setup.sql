-- Shared hosting (SmarterASP.NET) replacement for 00_create_database.sql.
--
-- On shared hosting the database and its user are created in the hosting control
-- panel, so 00 (CREATE DATABASE, CREATE LOGIN, USE [TheArqamSchools]) cannot run.
-- This script applies only the database settings from 00 to the database you are
-- connected to. Run it ONCE on the new, EMPTY database, before 01_schema.sql.
--
-- Order on SmarterASP.NET (connected to your database in SSMS):
--   1. this script
--   2. 01_schema.sql
--   3. 02_seed_data.sql
--   4. (skip 03_fulltext.sql: optional, and Full-Text Search may not be offered on shared hosting)
--   5. 04_simplify_navigation.sql
--   6. 05_franchise_header_button.sql
--
-- The website then connects with the database user from the control panel
-- (connection string in the GitHub secret DB_CONNECTION_STRING).

SET NOCOUNT ON;

IF DB_NAME() IN (N'master', N'model', N'msdb', N'tempdb')
BEGIN
    RAISERROR(N'Connect to your own website database first (not %s).', 16, 1, DB_NAME()) WITH NOWAIT;
    SET NOEXEC ON;
END;

IF OBJECT_ID(N'dbo.pages') IS NOT NULL
BEGIN
    RAISERROR(N'This database already has the website tables. Nothing changed.', 16, 1) WITH NOWAIT;
    SET NOEXEC ON;
END;

-- SQL Server 2025 is required: REGEXP_LIKE in the CHECK constraints needs level 170.
IF CAST(SERVERPROPERTY('ProductMajorVersion') AS INT) < 17
BEGIN
    RAISERROR(N'This server is not SQL Server 2025. Create the database again and choose MSSQL 2025.', 16, 1) WITH NOWAIT;
    SET NOEXEC ON;
END;

ALTER DATABASE CURRENT SET COMPATIBILITY_LEVEL = 170;
GO

-- Same collation as 00 (supplementary characters). Only possible while the database is empty.
BEGIN TRY
    ALTER DATABASE CURRENT COLLATE Latin1_General_100_CI_AS_SC;
END TRY
BEGIN CATCH
    PRINT N'Note: collation not changed (' + ERROR_MESSAGE() + N'). The site still works.';
END CATCH;
GO

-- Same reading behaviour as 00: readers do not block writers.
BEGIN TRY
    ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE;
    ALTER DATABASE CURRENT SET ALLOW_SNAPSHOT_ISOLATION ON;
END TRY
BEGIN CATCH
    PRINT N'Note: snapshot settings not changed (' + ERROR_MESSAGE() + N'). The site still works.';
END CATCH;
GO

SELECT DB_NAME() AS database_name,
       compatibility_level,
       collation_name,
       is_read_committed_snapshot_on,
       CAST(SERVERPROPERTY('ProductVersion') AS NVARCHAR(50)) AS server_version
FROM sys.databases
WHERE name = DB_NAME();
GO
SET NOEXEC OFF;
