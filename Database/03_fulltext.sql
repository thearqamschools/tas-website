-- =============================================================================
-- THE ARQAM SCHOOLS: SITE SEARCH (optional, SQL Server Full-Text Search)
-- Needs the "Full-Text and Semantic Extractions for Search" feature, which is
-- part of SQL Server 2025 (and Express with Advanced Services). Without it the
-- website falls back to simple LIKE searches.
-- =============================================================================
IF FULLTEXTSERVICEPROPERTY('IsFullTextInstalled') = 1
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = N'arqam_search')
        CREATE FULLTEXT CATALOG [arqam_search] AS DEFAULT;

    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.news'))
        CREATE FULLTEXT INDEX ON [dbo].[news] ([title], [excerpt]) KEY INDEX [PK_news] WITH CHANGE_TRACKING AUTO;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.events'))
        CREATE FULLTEXT INDEX ON [dbo].[events] ([title], [summary], [venue]) KEY INDEX [PK_events] WITH CHANGE_TRACKING AUTO;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.branches'))
        CREATE FULLTEXT INDEX ON [dbo].[branches] ([name], [area], [address_line], [overview]) KEY INDEX [PK_branches] WITH CHANGE_TRACKING AUTO;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.downloads'))
        CREATE FULLTEXT INDEX ON [dbo].[downloads] ([title], [description]) KEY INDEX [PK_downloads] WITH CHANGE_TRACKING AUTO;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.achievements'))
        CREATE FULLTEXT INDEX ON [dbo].[achievements] ([title], [summary]) KEY INDEX [PK_achievements] WITH CHANGE_TRACKING AUTO;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.job_postings'))
        CREATE FULLTEXT INDEX ON [dbo].[job_postings] ([title], [department], [subject], [location_text], [summary]) KEY INDEX [PK_job_postings] WITH CHANGE_TRACKING AUTO;
    IF NOT EXISTS (SELECT 1 FROM sys.fulltext_indexes WHERE object_id = OBJECT_ID(N'dbo.pages'))
        CREATE FULLTEXT INDEX ON [dbo].[pages] ([title], [summary]) KEY INDEX [PK_pages] WITH CHANGE_TRACKING AUTO;
END
ELSE
    PRINT N'Full-Text Search is not installed: site search will use LIKE.';
GO

-- Audit log: newest-first browsing (replaces the PostgreSQL BRIN index).
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'audit_logs_created_idx')
    CREATE NONCLUSTERED INDEX [audit_logs_created_idx] ON [dbo].[audit_logs] ([created_at] DESC);
GO
