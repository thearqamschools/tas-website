-- =============================================================================
-- THE ARQAM SCHOOLS: DATABASE SCHEMA FOR SQL SERVER 2025
-- Generated from the website's PostgreSQL schema (20 migrations) by
-- Database/tools/convert.py. Run after 00_create_database.sql.
--
-- Type mapping: uuid -> UNIQUEIDENTIFIER, timestamptz -> DATETIMEOFFSET(3),
-- boolean -> BIT, jsonb and text[] -> NVARCHAR(MAX) holding JSON (ISJSON
-- checks), enums -> NVARCHAR with CHECK (... IN ...), text -> NVARCHAR(n).
-- Pattern checks use REGEXP_LIKE (SQL Server 2025, compatibility level 170).
--
-- Moved to the application (ASP.NET MVC), because SQL Server does them
-- differently: permission checks (PostgreSQL row level security), audit log,
-- status history, reference numbers, updated_at/updated_by, "media in use"
-- protection, and clearing references when a row is deleted (ON DELETE SET
-- NULL becomes NO ACTION; most content rows are soft-deleted anyway).
-- =============================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
GO

CREATE TABLE [dbo].[achievements] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_achievements_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [type] NVARCHAR(15) NOT NULL,
    [title] NVARCHAR(160) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [summary] NVARCHAR(300) NULL,
    [body] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_achievements_body] DEFAULT N'{}',
    [student_name] NVARCHAR(120) NULL,
    [class_level_id] UNIQUEIDENTIFIER NULL,
    [academic_year] NVARCHAR(7) NULL,
    [achieved_on] DATE NULL,
    [exam_board] NVARCHAR(120) NULL,
    [metrics] NVARCHAR(MAX) NULL,
    [media_id] UNIQUEIDENTIFIER NULL,
    [consent_confirmed] BIT NOT NULL CONSTRAINT [DF_achievements_consent_confirmed] DEFAULT 0,
    [is_featured] BIT NOT NULL CONSTRAINT [DF_achievements_is_featured] DEFAULT 0,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_achievements_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_achievements_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_achievements_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_achievements] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_achievements_type_values] CHECK ([type] IN (N'exam_result', N'student', N'award', N'sports', N'competition', N'other')),
    CONSTRAINT [CK_achievements_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_achievements_body_json] CHECK ([body] IS NULL OR ISJSON([body], VALUE) = 1),
    CONSTRAINT [CK_achievements_academic_year_format] CHECK (REGEXP_LIKE([academic_year], N'^[0-9]{4}-[0-9]{2}$')),
    CONSTRAINT [CK_achievements_metrics_json] CHECK ([metrics] IS NULL OR ISJSON([metrics], VALUE) = 1),
    CONSTRAINT [CK_achievements_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_achievements_body_check] CHECK ((ISJSON([body], OBJECT) = 1)),
    CONSTRAINT [CK_achievements_consent] CHECK ((([status] <> N'published') OR (([student_name] IS NULL) AND ([media_id] IS NULL)) OR [consent_confirmed] = 1)),
    CONSTRAINT [CK_achievements_metrics_check] CHECK ((([metrics] IS NULL) OR (ISJSON([metrics], OBJECT) = 1))),
    CONSTRAINT [CK_achievements_title_check] CHECK (LEN([title]) >= 3)
);
GO

CREATE TABLE [dbo].[admission_cycles] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_admission_cycles_id] DEFAULT NEWSEQUENTIALID(),
    [name] NVARCHAR(120) NOT NULL,
    [academic_year] NVARCHAR(7) NOT NULL,
    [opens_on] DATE NOT NULL,
    [closes_on] DATE NULL,
    [is_active] BIT NOT NULL CONSTRAINT [DF_admission_cycles_is_active] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_cycles_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_cycles_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_admission_cycles] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_admission_cycles_academic_year_format] CHECK (REGEXP_LIKE([academic_year], N'^[0-9]{4}-[0-9]{2}$')),
    CONSTRAINT [CK_admission_cycles_dates] CHECK ((([closes_on] IS NULL) OR ([closes_on] >= [opens_on])))
);
GO

CREATE TABLE [dbo].[admission_documents] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_admission_documents_id] DEFAULT NEWSEQUENTIALID(),
    [admission_id] UNIQUEIDENTIFIER NOT NULL,
    [document_type] NVARCHAR(450) NOT NULL,
    [media_id] UNIQUEIDENTIFIER NOT NULL,
    [is_verified] BIT NOT NULL CONSTRAINT [DF_admission_documents_is_verified] DEFAULT 0,
    [verified_by] UNIQUEIDENTIFIER NULL,
    [verified_at] DATETIMEOFFSET(3) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_documents_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_documents_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_admission_documents] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_admission_documents_document_type_check] CHECK ((REGEXP_LIKE([document_type], N'^[a-z_]{2,40}$')))
);
GO

CREATE TABLE [dbo].[admission_inquiries] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_admission_inquiries_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [class_level_id] UNIQUEIDENTIFIER NULL,
    [name] NVARCHAR(120) NOT NULL,
    [phone] NVARCHAR(20) NOT NULL,
    [email] NVARCHAR(254) NULL,
    [message] NVARCHAR(2000) NULL,
    [status] NVARCHAR(15) NOT NULL CONSTRAINT [DF_admission_inquiries_status] DEFAULT N'new',
    [assigned_to] UNIQUEIDENTIFIER NULL,
    [source_page] NVARCHAR(300) NULL,
    [converted_admission_id] UNIQUEIDENTIFIER NULL,
    [resolved_at] DATETIMEOFFSET(3) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_inquiries_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_inquiries_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_admission_inquiries] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_admission_inquiries_phone_format] CHECK (REGEXP_LIKE([phone], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_admission_inquiries_email_format] CHECK (REGEXP_LIKE([email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_admission_inquiries_status_values] CHECK ([status] IN (N'new', N'in_progress', N'resolved', N'spam')),
    CONSTRAINT [CK_admission_inquiries_name_check] CHECK (LEN([name]) >= 2)
);
GO

CREATE TABLE [dbo].[admission_notes] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_admission_notes_id] DEFAULT NEWSEQUENTIALID(),
    [admission_id] UNIQUEIDENTIFIER NOT NULL,
    [author_id] UNIQUEIDENTIFIER NULL,
    [body] NVARCHAR(4000) NOT NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_notes_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_notes_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_admission_notes] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_admission_notes_body_check] CHECK (LEN([body]) >= 1)
);
GO

CREATE TABLE [dbo].[admission_stages] (
    [key] NVARCHAR(450) NOT NULL,
    [label] NVARCHAR(60) NOT NULL,
    [public_label] NVARCHAR(60) NOT NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_admission_stages_sort_order] DEFAULT 0,
    [is_terminal] BIT NOT NULL CONSTRAINT [DF_admission_stages_is_terminal] DEFAULT 0,
    [is_initial] BIT NOT NULL CONSTRAINT [DF_admission_stages_is_initial] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_stages_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_stages_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_admission_stages] PRIMARY KEY CLUSTERED ([key]),
    CONSTRAINT [CK_admission_stages_key_check] CHECK ((REGEXP_LIKE([key], N'^[a-z_]{2,40}$')))
);
GO

CREATE TABLE [dbo].[admission_status_history] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [admission_id] UNIQUEIDENTIFIER NOT NULL,
    [from_stage] NVARCHAR(450) NULL,
    [to_stage] NVARCHAR(450) NOT NULL,
    [note] NVARCHAR(1000) NULL,
    [changed_by] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admission_status_history_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_admission_status_history] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [dbo].[admissions] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_admissions_id] DEFAULT NEWSEQUENTIALID(),
    [reference_no] NVARCHAR(450) NOT NULL,
    [cycle_id] UNIQUEIDENTIFIER NOT NULL,
    [branch_id] UNIQUEIDENTIFIER NOT NULL,
    [class_level_id] UNIQUEIDENTIFIER NOT NULL,
    [stage_key] NVARCHAR(450) NOT NULL,
    [student_first_name] NVARCHAR(80) NOT NULL,
    [student_last_name] NVARCHAR(80) NOT NULL,
    [student_date_of_birth] DATE NOT NULL,
    [student_gender] NVARCHAR(20) NULL,
    [previous_school] NVARCHAR(200) NULL,
    [guardian_name] NVARCHAR(120) NOT NULL,
    [guardian_relationship] NVARCHAR(40) NOT NULL,
    [guardian_phone] NVARCHAR(20) NOT NULL,
    [guardian_email] NVARCHAR(254) NULL,
    [address] NVARCHAR(400) NULL,
    [details] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_admissions_details] DEFAULT N'{}',
    [assigned_to] UNIQUEIDENTIFIER NULL,
    [source] NVARCHAR(60) NULL,
    [inquiry_id] UNIQUEIDENTIFIER NULL,
    [consent_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admissions_consent_at] DEFAULT SYSDATETIMEOFFSET(),
    [submitted_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admissions_submitted_at] DEFAULT SYSDATETIMEOFFSET(),
    [retention_until] DATE NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admissions_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_admissions_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_admissions] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_admissions_guardian_phone_format] CHECK (REGEXP_LIKE([guardian_phone], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_admissions_guardian_email_format] CHECK (REGEXP_LIKE([guardian_email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_admissions_details_json] CHECK ([details] IS NULL OR ISJSON([details], VALUE) = 1),
    CONSTRAINT [CK_admissions_details_check] CHECK ((ISJSON([details], OBJECT) = 1)),
    CONSTRAINT [CK_admissions_guardian_name_check] CHECK (LEN([guardian_name]) >= 2),
    CONSTRAINT [CK_admissions_student_date_of_birth_check] CHECK (([student_date_of_birth] > N'1990-01-01')),
    CONSTRAINT [CK_admissions_student_first_name_check] CHECK (LEN([student_first_name]) >= 1),
    CONSTRAINT [CK_admissions_student_last_name_check] CHECK (LEN([student_last_name]) >= 1)
);
GO

CREATE TABLE [dbo].[audit_logs] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [actor_id] UNIQUEIDENTIFIER NULL,
    [action] NVARCHAR(MAX) NOT NULL,
    [table_name] NVARCHAR(450) NOT NULL,
    [record_id] NVARCHAR(450) NULL,
    [changes] NVARCHAR(MAX) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_audit_logs_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_audit_logs] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_audit_logs_changes_json] CHECK ([changes] IS NULL OR ISJSON([changes], VALUE) = 1),
    CONSTRAINT [CK_audit_logs_action_check] CHECK (([action] IN (N'insert', N'update', N'delete')))
);
GO

CREATE TABLE [dbo].[branch_classes] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_branch_classes_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NOT NULL,
    [class_level_id] UNIQUEIDENTIFIER NOT NULL,
    [is_admission_open] BIT NOT NULL CONSTRAINT [DF_branch_classes_is_admission_open] DEFAULT 0,
    [seats_total] INT NULL,
    [seats_available] INT NULL,
    [shift] NVARCHAR(40) NULL,
    [note] NVARCHAR(300) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_branch_classes_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_branch_classes_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_branch_classes] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_branch_classes_seats] CHECK ((([seats_available] IS NULL) OR ([seats_total] IS NULL) OR ([seats_available] <= [seats_total]))),
    CONSTRAINT [CK_branch_classes_seats_available_check] CHECK (([seats_available] >= 0)),
    CONSTRAINT [CK_branch_classes_seats_total_check] CHECK (([seats_total] >= 0))
);
GO

CREATE TABLE [dbo].[branch_facilities] (
    [branch_id] UNIQUEIDENTIFIER NOT NULL,
    [facility_id] UNIQUEIDENTIFIER NOT NULL,
    [note] NVARCHAR(200) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_branch_facilities_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_branch_facilities_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_branch_facilities] PRIMARY KEY CLUSTERED ([branch_id], [facility_id])
);
GO

CREATE TABLE [dbo].[branch_images] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_branch_images_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NOT NULL,
    [media_id] UNIQUEIDENTIFIER NOT NULL,
    [caption] NVARCHAR(300) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_branch_images_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_branch_images_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_branch_images_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_branch_images] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [dbo].[branches] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_branches_id] DEFAULT NEWSEQUENTIALID(),
    [city_id] UNIQUEIDENTIFIER NOT NULL,
    [code] NVARCHAR(450) NULL,
    [name] NVARCHAR(150) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [area] NVARCHAR(120) NULL,
    [address_line] NVARCHAR(300) NULL,
    [postal_code] NVARCHAR(20) NULL,
    [latitude] DECIMAL(9,6) NULL,
    [longitude] DECIMAL(9,6) NULL,
    [phone] NVARCHAR(20) NULL,
    [phone_alt] NVARCHAR(20) NULL,
    [whatsapp] NVARCHAR(20) NULL,
    [email] NVARCHAR(254) NULL,
    [timings] NVARCHAR(200) NULL,
    [overview] NVARCHAR(MAX) NULL,
    [principal_name] NVARCHAR(120) NULL,
    [principal_title] NVARCHAR(120) NULL,
    [principal_message] NVARCHAR(MAX) NULL,
    [principal_media_id] UNIQUEIDENTIFIER NULL,
    [cover_media_id] UNIQUEIDENTIFIER NULL,
    [status] NVARCHAR(15) NOT NULL CONSTRAINT [DF_branches_status] DEFAULT N'active',
    [is_admission_open] BIT NOT NULL CONSTRAINT [DF_branches_is_admission_open] DEFAULT 0,
    [show_staff] BIT NOT NULL CONSTRAINT [DF_branches_show_staff] DEFAULT 0,
    [opened_on] DATE NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_branches_sort_order] DEFAULT 0,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_branches_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_branches_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    [is_published] BIT NOT NULL CONSTRAINT [DF_branches_is_published] DEFAULT 0,
    [published_at] DATETIMEOFFSET(3) NULL,
    [archived_at] DATETIMEOFFSET(3) NULL,
    [admission_status] NVARCHAR(18) NOT NULL CONSTRAINT [DF_branches_admission_status] DEFAULT N'closed',
    [admission_note] NVARCHAR(200) NULL,
    [google_maps_url] NVARCHAR(500) NULL,
    -- area_slug: set by the application from [area] (lowercase, hyphens)
    [area_slug] NVARCHAR(120) NULL,
    [social_links] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_branches_social_links] DEFAULT N'[]',
    CONSTRAINT [PK_branches] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_branches_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_branches_phone_format] CHECK (REGEXP_LIKE([phone], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_branches_phone_alt_format] CHECK (REGEXP_LIKE([phone_alt], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_branches_whatsapp_format] CHECK (REGEXP_LIKE([whatsapp], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_branches_email_format] CHECK (REGEXP_LIKE([email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_branches_status_values] CHECK ([status] IN (N'active', N'coming_soon', N'inactive')),
    CONSTRAINT [CK_branches_admission_status_values] CHECK ([admission_status] IN (N'open', N'closed', N'waiting_list', N'opening_soon', N'contact_campus')),
    CONSTRAINT [CK_branches_social_links_json] CHECK ([social_links] IS NULL OR ISJSON([social_links], VALUE) = 1),
    CONSTRAINT [CK_branches_code_check] CHECK ((REGEXP_LIKE([code], N'^[A-Z0-9-]{2,20}$'))),
    CONSTRAINT [CK_branches_google_maps_url_check] CHECK (((LEN([google_maps_url]) <= 500) AND (REGEXP_LIKE([google_maps_url], N'^https://(www\.)?(google\.[a-z.]+/maps|maps\.google\.[a-z.]+|maps\.app\.goo\.gl/|goo\.gl/maps/)')))),
    CONSTRAINT [CK_branches_latitude_check] CHECK ((([latitude] >= -90) AND ([latitude] <= 90))),
    CONSTRAINT [CK_branches_longitude_check] CHECK ((([longitude] >= -180) AND ([longitude] <= 180))),
    CONSTRAINT [CK_branches_name_check] CHECK (LEN([name]) >= 2),
    CONSTRAINT [CK_branches_social_links_check] CHECK (((ISJSON([social_links], ARRAY) = 1) AND (1 = 1)))
);
GO

CREATE TABLE [dbo].[categories] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_categories_id] DEFAULT NEWSEQUENTIALID(),
    [type] NVARCHAR(12) NOT NULL,
    [name] NVARCHAR(80) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [description] NVARCHAR(300) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_categories_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_categories_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_categories_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_categories] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_categories_type_values] CHECK ([type] IN (N'news', N'event', N'gallery', N'download')),
    CONSTRAINT [CK_categories_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$'))
);
GO

CREATE TABLE [dbo].[cities] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_cities_id] DEFAULT NEWSEQUENTIALID(),
    [region_id] UNIQUEIDENTIFIER NULL,
    [name] NVARCHAR(100) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [intro] NVARCHAR(MAX) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_cities_sort_order] DEFAULT 0,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_cities_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_cities_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [district_id] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_cities] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_cities_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$'))
);
GO

CREATE TABLE [dbo].[class_levels] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_class_levels_id] DEFAULT NEWSEQUENTIALID(),
    [name] NVARCHAR(80) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [stage] NVARCHAR(60) NOT NULL,
    [min_age_months] INT NULL,
    [max_age_months] INT NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_class_levels_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_class_levels_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_class_levels_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_class_levels] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_class_levels_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_class_levels_age_range] CHECK ((([max_age_months] IS NULL) OR ([min_age_months] IS NULL) OR ([max_age_months] >= [min_age_months]))),
    CONSTRAINT [CK_class_levels_max_age_months_check] CHECK ((([max_age_months] >= 0) AND ([max_age_months] <= 300))),
    CONSTRAINT [CK_class_levels_min_age_months_check] CHECK ((([min_age_months] >= 0) AND ([min_age_months] <= 300)))
);
GO

CREATE TABLE [dbo].[contact_messages] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_contact_messages_id] DEFAULT NEWSEQUENTIALID(),
    [type] NVARCHAR(14) NOT NULL CONSTRAINT [DF_contact_messages_type] DEFAULT N'general',
    [branch_id] UNIQUEIDENTIFIER NULL,
    [name] NVARCHAR(120) NOT NULL,
    [email] NVARCHAR(254) NULL,
    [phone] NVARCHAR(20) NULL,
    [subject] NVARCHAR(200) NULL,
    [message] NVARCHAR(MAX) NOT NULL,
    [status] NVARCHAR(15) NOT NULL CONSTRAINT [DF_contact_messages_status] DEFAULT N'new',
    [assigned_to] UNIQUEIDENTIFIER NULL,
    [source_page] NVARCHAR(300) NULL,
    [replied_at] DATETIMEOFFSET(3) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_contact_messages_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_contact_messages_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_contact_messages] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_contact_messages_type_values] CHECK ([type] IN (N'general', N'admissions', N'careers', N'feedback', N'media', N'other')),
    CONSTRAINT [CK_contact_messages_email_format] CHECK (REGEXP_LIKE([email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_contact_messages_phone_format] CHECK (REGEXP_LIKE([phone], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_contact_messages_status_values] CHECK ([status] IN (N'new', N'in_progress', N'resolved', N'spam')),
    CONSTRAINT [CK_contact_messages_message_check] CHECK (LEN([message]) >= 2),
    CONSTRAINT [CK_contact_messages_name_check] CHECK (LEN([name]) >= 2),
    CONSTRAINT [CK_contact_messages_reachable] CHECK ((((CASE WHEN [email] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [phone] IS NULL THEN 0 ELSE 1 END)) >= 1))
);
GO

CREATE TABLE [dbo].[districts] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_districts_id] DEFAULT NEWSEQUENTIALID(),
    [region_id] UNIQUEIDENTIFIER NULL,
    [name] NVARCHAR(100) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_districts_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_districts_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_districts_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_districts] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_districts_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$'))
);
GO

CREATE TABLE [dbo].[download_stats] (
    [download_id] UNIQUEIDENTIFIER NOT NULL,
    [download_count] BIGINT NOT NULL CONSTRAINT [DF_download_stats_download_count] DEFAULT 0,
    [last_download_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_download_stats] PRIMARY KEY CLUSTERED ([download_id]),
    CONSTRAINT [CK_download_stats_download_count_check] CHECK (([download_count] >= 0))
);
GO

CREATE TABLE [dbo].[downloads] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_downloads_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [category_id] UNIQUEIDENTIFIER NULL,
    [media_id] UNIQUEIDENTIFIER NOT NULL,
    [title] NVARCHAR(160) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [description] NVARCHAR(500) NULL,
    [academic_year] NVARCHAR(7) NULL,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_downloads_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_downloads_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_downloads_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_downloads] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_downloads_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_downloads_academic_year_format] CHECK (REGEXP_LIKE([academic_year], N'^[0-9]{4}-[0-9]{2}$')),
    CONSTRAINT [CK_downloads_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_downloads_title_check] CHECK (LEN([title]) >= 3)
);
GO

CREATE TABLE [dbo].[events] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_events_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [category_id] UNIQUEIDENTIFIER NULL,
    [title] NVARCHAR(160) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [summary] NVARCHAR(300) NULL,
    [body] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_events_body] DEFAULT N'{}',
    [starts_at] DATETIMEOFFSET(3) NOT NULL,
    [ends_at] DATETIMEOFFSET(3) NULL,
    [is_all_day] BIT NOT NULL CONSTRAINT [DF_events_is_all_day] DEFAULT 0,
    [timezone] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_events_timezone] DEFAULT N'UTC',
    [venue] NVARCHAR(200) NULL,
    [location_url] NVARCHAR(MAX) NULL,
    [registration_url] NVARCHAR(MAX) NULL,
    [cover_media_id] UNIQUEIDENTIFIER NULL,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_events_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_events_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_events_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_events] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_events_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_events_body_json] CHECK ([body] IS NULL OR ISJSON([body], VALUE) = 1),
    CONSTRAINT [CK_events_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_events_body_check] CHECK ((ISJSON([body], OBJECT) = 1)),
    CONSTRAINT [CK_events_dates] CHECK ((([ends_at] IS NULL) OR ([ends_at] >= [starts_at]))),
    CONSTRAINT [CK_events_location_url_check] CHECK ((REGEXP_LIKE([location_url], N'^https://'))),
    CONSTRAINT [CK_events_registration_url_check] CHECK ((REGEXP_LIKE([registration_url], N'^https://'))),
    CONSTRAINT [CK_events_title_check] CHECK (LEN([title]) >= 3)
);
GO

CREATE TABLE [dbo].[facilities] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_facilities_id] DEFAULT NEWSEQUENTIALID(),
    [name] NVARCHAR(80) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [icon] NVARCHAR(MAX) NULL,
    [description] NVARCHAR(300) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_facilities_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_facilities_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_facilities_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_facilities] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_facilities_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_facilities_icon_check] CHECK ((REGEXP_LIKE([icon], N'^[a-z0-9-]{1,40}$')))
);
GO

CREATE TABLE [dbo].[franchise_applications] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_franchise_applications_id] DEFAULT NEWSEQUENTIALID(),
    [reference_no] NVARCHAR(450) NOT NULL,
    [status] NVARCHAR(21) NOT NULL CONSTRAINT [DF_franchise_applications_status] DEFAULT N'new',
    [full_name] NVARCHAR(120) NOT NULL,
    [mobile] NVARCHAR(20) NOT NULL,
    [whatsapp] NVARCHAR(20) NULL,
    [email] NVARCHAR(254) NOT NULL,
    [city] NVARCHAR(80) NOT NULL,
    [district] NVARCHAR(80) NULL,
    [province] NVARCHAR(80) NULL,
    [address] NVARCHAR(400) NULL,
    [proposed_city] NVARCHAR(80) NOT NULL,
    [proposed_area] NVARCHAR(120) NOT NULL,
    [building_plan] NVARCHAR(13) NULL,
    [building_area] NVARCHAR(80) NULL,
    [experience] NVARCHAR(2000) NULL,
    [investment_range] NVARCHAR(80) NULL,
    [start_timeline] NVARCHAR(80) NULL,
    [message] NVARCHAR(3000) NULL,
    [assigned_to] UNIQUEIDENTIFIER NULL,
    [source_page] NVARCHAR(300) NULL,
    [consent_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_applications_consent_at] DEFAULT SYSDATETIMEOFFSET(),
    [submitted_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_applications_submitted_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_applications_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_applications_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_franchise_applications] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_franchise_applications_status_values] CHECK ([status] IN (N'new', N'under_review', N'contacted', N'meeting_scheduled', N'approved', N'rejected')),
    CONSTRAINT [CK_franchise_applications_mobile_format] CHECK (REGEXP_LIKE([mobile], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_franchise_applications_whatsapp_format] CHECK (REGEXP_LIKE([whatsapp], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_franchise_applications_email_format] CHECK (REGEXP_LIKE([email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_franchise_applications_building_plan_values] CHECK ([building_plan] IN (N'own', N'rent', N'undecided')),
    CONSTRAINT [CK_franchise_applications_city_check] CHECK (LEN([city]) >= 2),
    CONSTRAINT [CK_franchise_applications_full_name_check] CHECK (LEN([full_name]) >= 2),
    CONSTRAINT [CK_franchise_applications_proposed_area_check] CHECK (LEN([proposed_area]) >= 2),
    CONSTRAINT [CK_franchise_applications_proposed_city_check] CHECK (LEN([proposed_city]) >= 2)
);
GO

CREATE TABLE [dbo].[franchise_notes] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_franchise_notes_id] DEFAULT NEWSEQUENTIALID(),
    [application_id] UNIQUEIDENTIFIER NOT NULL,
    [author_id] UNIQUEIDENTIFIER NULL,
    [body] NVARCHAR(4000) NOT NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_notes_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_notes_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_franchise_notes] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_franchise_notes_body_check] CHECK (LEN([body]) >= 1)
);
GO

CREATE TABLE [dbo].[franchise_settings] (
    [id] BIT NOT NULL CONSTRAINT [DF_franchise_settings_id] DEFAULT 1,
    [accepting_applications] BIT NOT NULL CONSTRAINT [DF_franchise_settings_accepting_applications] DEFAULT 1,
    [investment_ranges] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_franchise_settings_investment_ranges] DEFAULT N'[]',
    [start_timelines] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_franchise_settings_start_timelines] DEFAULT N'[]',
    [response_time] NVARCHAR(60) NULL,
    [closed_message] NVARCHAR(300) NULL,
    [notify_email] NVARCHAR(254) NULL,
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_settings_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_by] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_settings_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_franchise_settings] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_franchise_settings_investment_ranges_json] CHECK ([investment_ranges] IS NULL OR ISJSON([investment_ranges], VALUE) = 1),
    CONSTRAINT [CK_franchise_settings_start_timelines_json] CHECK ([start_timelines] IS NULL OR ISJSON([start_timelines], VALUE) = 1),
    CONSTRAINT [CK_franchise_settings_notify_email_format] CHECK (REGEXP_LIKE([notify_email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_franchise_settings_single_row] CHECK ([id] = 1),
    CONSTRAINT [CK_franchise_settings_investment_ranges_check] CHECK ((1 = 1)),
    CONSTRAINT [CK_franchise_settings_start_timelines_check] CHECK ((1 = 1))
);
GO

CREATE TABLE [dbo].[franchise_status_history] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [application_id] UNIQUEIDENTIFIER NOT NULL,
    [from_status] NVARCHAR(21) NULL,
    [to_status] NVARCHAR(21) NOT NULL,
    [note] NVARCHAR(1000) NULL,
    [changed_by] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_franchise_status_history_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_franchise_status_history] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_franchise_status_history_from_status_values] CHECK ([from_status] IN (N'new', N'under_review', N'contacted', N'meeting_scheduled', N'approved', N'rejected')),
    CONSTRAINT [CK_franchise_status_history_to_status_values] CHECK ([to_status] IN (N'new', N'under_review', N'contacted', N'meeting_scheduled', N'approved', N'rejected'))
);
GO

CREATE TABLE [dbo].[gallery_albums] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_gallery_albums_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [category_id] UNIQUEIDENTIFIER NULL,
    [event_id] UNIQUEIDENTIFIER NULL,
    [title] NVARCHAR(160) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [description] NVARCHAR(1000) NULL,
    [album_date] DATE NULL,
    [cover_media_id] UNIQUEIDENTIFIER NULL,
    [is_featured] BIT NOT NULL CONSTRAINT [DF_gallery_albums_is_featured] DEFAULT 0,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_gallery_albums_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_gallery_albums_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_gallery_albums_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_gallery_albums] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_gallery_albums_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_gallery_albums_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_gallery_albums_title_check] CHECK (LEN([title]) >= 3)
);
GO

CREATE TABLE [dbo].[gallery_images] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_gallery_images_id] DEFAULT NEWSEQUENTIALID(),
    [album_id] UNIQUEIDENTIFIER NOT NULL,
    [media_id] UNIQUEIDENTIFIER NOT NULL,
    [caption] NVARCHAR(300) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_gallery_images_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_gallery_images_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_gallery_images_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_gallery_images] PRIMARY KEY CLUSTERED ([id])
);
GO

CREATE TABLE [dbo].[head_offices] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_head_offices_id] DEFAULT NEWSEQUENTIALID(),
    [office_number] INT NOT NULL,
    [name] NVARCHAR(120) NOT NULL,
    [city] NVARCHAR(80) NULL,
    [address] NVARCHAR(300) NULL,
    [phone] NVARCHAR(20) NULL,
    [phone_alt] NVARCHAR(20) NULL,
    [whatsapp] NVARCHAR(20) NULL,
    [email] NVARCHAR(254) NULL,
    [google_maps_url] NVARCHAR(500) NULL,
    [hours] NVARCHAR(200) NULL,
    [is_published] BIT NOT NULL CONSTRAINT [DF_head_offices_is_published] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_head_offices_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_head_offices_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_head_offices] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_head_offices_phone_format] CHECK (REGEXP_LIKE([phone], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_head_offices_phone_alt_format] CHECK (REGEXP_LIKE([phone_alt], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_head_offices_whatsapp_format] CHECK (REGEXP_LIKE([whatsapp], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_head_offices_email_format] CHECK (REGEXP_LIKE([email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_head_offices_google_maps_url_check] CHECK (((REGEXP_LIKE([google_maps_url], N'^https://')) AND (LEN([google_maps_url]) <= 500))),
    CONSTRAINT [CK_head_offices_name_check] CHECK (LEN([name]) >= 2),
    CONSTRAINT [CK_head_offices_office_number_check] CHECK ((([office_number] >= 1) AND ([office_number] <= 9)))
);
GO

CREATE TABLE [dbo].[homepage_content] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_homepage_content_id] DEFAULT NEWSEQUENTIALID(),
    [section_key] NVARCHAR(450) NOT NULL,
    [type] NVARCHAR(MAX) NOT NULL,
    [data] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_homepage_content_data] DEFAULT N'{}',
    [draft_data] NVARCHAR(MAX) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_homepage_content_sort_order] DEFAULT 0,
    [is_visible] BIT NOT NULL CONSTRAINT [DF_homepage_content_is_visible] DEFAULT 1,
    [is_locked] BIT NOT NULL CONSTRAINT [DF_homepage_content_is_locked] DEFAULT 0,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_homepage_content_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_homepage_content_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_homepage_content_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_homepage_content] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_homepage_content_data_json] CHECK ([data] IS NULL OR ISJSON([data], VALUE) = 1),
    CONSTRAINT [CK_homepage_content_draft_data_json] CHECK ([draft_data] IS NULL OR ISJSON([draft_data], VALUE) = 1),
    CONSTRAINT [CK_homepage_content_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_homepage_content_data_check] CHECK ((ISJSON([data], OBJECT) = 1)),
    CONSTRAINT [CK_homepage_content_draft_data_check] CHECK ((([draft_data] IS NULL) OR (ISJSON([draft_data], OBJECT) = 1))),
    CONSTRAINT [CK_homepage_content_section_key_check] CHECK ((REGEXP_LIKE([section_key], N'^[a-z_]{1,40}$'))),
    CONSTRAINT [CK_homepage_content_type_check] CHECK ((REGEXP_LIKE([type], N'^[a-z_]{1,40}$')))
);
GO

CREATE TABLE [dbo].[job_application_history] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [job_application_id] UNIQUEIDENTIFIER NOT NULL,
    [from_status] NVARCHAR(15) NULL,
    [to_status] NVARCHAR(15) NOT NULL,
    [note] NVARCHAR(1000) NULL,
    [changed_by] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_job_application_history_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_job_application_history] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_job_application_history_from_status_values] CHECK ([from_status] IN (N'received', N'shortlisted', N'interview', N'offered', N'hired', N'rejected', N'withdrawn')),
    CONSTRAINT [CK_job_application_history_to_status_values] CHECK ([to_status] IN (N'received', N'shortlisted', N'interview', N'offered', N'hired', N'rejected', N'withdrawn'))
);
GO

CREATE TABLE [dbo].[job_applications] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_job_applications_id] DEFAULT NEWSEQUENTIALID(),
    [job_posting_id] UNIQUEIDENTIFIER NULL,
    [branch_id] UNIQUEIDENTIFIER NULL,
    [full_name] NVARCHAR(120) NOT NULL,
    [email] NVARCHAR(254) NOT NULL,
    [phone] NVARCHAR(20) NOT NULL,
    [city] NVARCHAR(100) NULL,
    [highest_qualification] NVARCHAR(200) NULL,
    [experience_years] SMALLINT NULL,
    [cv_media_id] UNIQUEIDENTIFIER NULL,
    [cover_letter] NVARCHAR(MAX) NULL,
    [details] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_job_applications_details] DEFAULT N'{}',
    [status] NVARCHAR(15) NOT NULL CONSTRAINT [DF_job_applications_status] DEFAULT N'received',
    [rating] SMALLINT NULL,
    [assigned_to] UNIQUEIDENTIFIER NULL,
    [consent_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_job_applications_consent_at] DEFAULT SYSDATETIMEOFFSET(),
    [retention_until] DATE NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_job_applications_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_job_applications_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_job_applications] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_job_applications_email_format] CHECK (REGEXP_LIKE([email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_job_applications_phone_format] CHECK (REGEXP_LIKE([phone], N'^\+?[0-9 ()-]{7,20}$')),
    CONSTRAINT [CK_job_applications_details_json] CHECK ([details] IS NULL OR ISJSON([details], VALUE) = 1),
    CONSTRAINT [CK_job_applications_status_values] CHECK ([status] IN (N'received', N'shortlisted', N'interview', N'offered', N'hired', N'rejected', N'withdrawn')),
    CONSTRAINT [CK_job_applications_details_check] CHECK ((ISJSON([details], OBJECT) = 1)),
    CONSTRAINT [CK_job_applications_experience_years_check] CHECK ((([experience_years] >= 0) AND ([experience_years] <= 60))),
    CONSTRAINT [CK_job_applications_full_name_check] CHECK (LEN([full_name]) >= 2),
    CONSTRAINT [CK_job_applications_rating_check] CHECK ((([rating] >= 1) AND ([rating] <= 5)))
);
GO

CREATE TABLE [dbo].[job_postings] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_job_postings_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [title] NVARCHAR(160) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [department] NVARCHAR(100) NULL,
    [subject] NVARCHAR(100) NULL,
    [employment_type] NVARCHAR(14) NOT NULL,
    [location_text] NVARCHAR(150) NULL,
    [experience_min_years] SMALLINT NULL,
    [qualifications] NVARCHAR(500) NULL,
    [summary] NVARCHAR(300) NULL,
    [description] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_job_postings_description] DEFAULT N'{}',
    [requirements] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_job_postings_requirements] DEFAULT N'{}',
    [positions] SMALLINT NOT NULL CONSTRAINT [DF_job_postings_positions] DEFAULT 1,
    [closes_on] DATE NULL,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_job_postings_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_job_postings_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_job_postings_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_job_postings] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_job_postings_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_job_postings_employment_type_values] CHECK ([employment_type] IN (N'full_time', N'part_time', N'contract', N'visiting', N'internship')),
    CONSTRAINT [CK_job_postings_description_json] CHECK ([description] IS NULL OR ISJSON([description], VALUE) = 1),
    CONSTRAINT [CK_job_postings_requirements_json] CHECK ([requirements] IS NULL OR ISJSON([requirements], VALUE) = 1),
    CONSTRAINT [CK_job_postings_status_values] CHECK ([status] IN (N'draft', N'published', N'closed', N'archived')),
    CONSTRAINT [CK_job_postings_description_check] CHECK ((ISJSON([description], OBJECT) = 1)),
    CONSTRAINT [CK_job_postings_experience_min_years_check] CHECK ((([experience_min_years] >= 0) AND ([experience_min_years] <= 50))),
    CONSTRAINT [CK_job_postings_positions_check] CHECK ((([positions] >= 1) AND ([positions] <= 500))),
    CONSTRAINT [CK_job_postings_requirements_check] CHECK ((ISJSON([requirements], OBJECT) = 1)),
    CONSTRAINT [CK_job_postings_title_check] CHECK (LEN([title]) >= 3)
);
GO

CREATE TABLE [dbo].[media] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_media_id] DEFAULT NEWSEQUENTIALID(),
    [bucket] NVARCHAR(450) NOT NULL,
    [storage_path] NVARCHAR(512) NOT NULL,
    [visibility] AS CAST(CASE WHEN bucket LIKE N'public-%' THEN N'public' ELSE N'private' END AS NVARCHAR(10)) PERSISTED,
    [original_filename] NVARCHAR(255) NOT NULL,
    [mime_type] NVARCHAR(450) NOT NULL,
    [size_bytes] BIGINT NOT NULL,
    [width] INT NULL,
    [height] INT NULL,
    [alt_text] NVARCHAR(250) NULL,
    [caption] NVARCHAR(500) NULL,
    [blurhash] NVARCHAR(100) NULL,
    [checksum_sha256] NVARCHAR(450) NULL,
    [folder_id] UNIQUEIDENTIFIER NULL,
    [branch_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_media_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_media_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_media] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_media_visibility_values] CHECK ([visibility] IN (N'public', N'private')),
    CONSTRAINT [CK_media_bucket_check] CHECK (([bucket] IN (N'public-media', N'public-downloads', N'private-admissions', N'private-careers'))),
    CONSTRAINT [CK_media_checksum_sha256_check] CHECK ((REGEXP_LIKE([checksum_sha256], N'^[0-9a-f]{64}$'))),
    CONSTRAINT [CK_media_height_check] CHECK (([height] > 0)),
    CONSTRAINT [CK_media_mime_type_check] CHECK ((REGEXP_LIKE([mime_type], N'^[a-z]+/[a-z0-9.+-]+$'))),
    CONSTRAINT [CK_media_size_bytes_check] CHECK ((([size_bytes] > 0) AND ([size_bytes] <= 52428800))),
    CONSTRAINT [CK_media_storage_path_check] CHECK (((LEN([storage_path]) <= 512) AND (NOT REGEXP_LIKE([storage_path], N'\.\.')))),
    CONSTRAINT [CK_media_width_check] CHECK (([width] > 0))
);
GO

CREATE TABLE [dbo].[media_folders] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_media_folders_id] DEFAULT NEWSEQUENTIALID(),
    [parent_id] UNIQUEIDENTIFIER NULL,
    [branch_id] UNIQUEIDENTIFIER NULL,
    [name] NVARCHAR(100) NOT NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_media_folders_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_media_folders_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_media_folders_sort_order] DEFAULT 0,
    CONSTRAINT [PK_media_folders] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_media_folders_name_check] CHECK (LEN([name]) >= 1)
);
GO

CREATE TABLE [dbo].[navigation_items] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_navigation_items_id] DEFAULT NEWSEQUENTIALID(),
    [menu_id] UNIQUEIDENTIFIER NOT NULL,
    [parent_id] UNIQUEIDENTIFIER NULL,
    [label] NVARCHAR(60) NOT NULL,
    [page_id] UNIQUEIDENTIFIER NULL,
    [url] NVARCHAR(MAX) NULL,
    [target] NVARCHAR(9) NOT NULL CONSTRAINT [DF_navigation_items_target] DEFAULT N'self',
    [description] NVARCHAR(160) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_navigation_items_sort_order] DEFAULT 0,
    [is_visible] BIT NOT NULL CONSTRAINT [DF_navigation_items_is_visible] DEFAULT 1,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_navigation_items_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_navigation_items_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_navigation_items] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_navigation_items_target_values] CHECK ([target] IN (N'self', N'blank')),
    CONSTRAINT [CK_navigation_items_label_check] CHECK (LEN([label]) >= 1),
    CONSTRAINT [CK_navigation_items_link] CHECK ((((CASE WHEN [page_id] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [url] IS NULL THEN 0 ELSE 1 END)) = 1)),
    CONSTRAINT [CK_navigation_items_url_check] CHECK ((REGEXP_LIKE([url], N'^(/|https://)')))
);
GO

CREATE TABLE [dbo].[navigation_menus] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_navigation_menus_id] DEFAULT NEWSEQUENTIALID(),
    [key] NVARCHAR(450) NOT NULL,
    [name] NVARCHAR(80) NOT NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_navigation_menus_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_navigation_menus_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_navigation_menus] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_navigation_menus_key_check] CHECK ((REGEXP_LIKE([key], N'^[a-z_]{1,40}$')))
);
GO

CREATE TABLE [dbo].[news] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_news_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [category_id] UNIQUEIDENTIFIER NULL,
    [title] NVARCHAR(160) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [excerpt] NVARCHAR(300) NULL,
    [body] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_news_body] DEFAULT N'{}',
    [cover_media_id] UNIQUEIDENTIFIER NULL,
    [author_id] UNIQUEIDENTIFIER NULL,
    [is_featured] BIT NOT NULL CONSTRAINT [DF_news_is_featured] DEFAULT 0,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_news_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_news_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_news_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_news] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_news_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_news_body_json] CHECK ([body] IS NULL OR ISJSON([body], VALUE) = 1),
    CONSTRAINT [CK_news_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_news_body_check] CHECK ((ISJSON([body], OBJECT) = 1)),
    CONSTRAINT [CK_news_title_check] CHECK (LEN([title]) >= 3)
);
GO

CREATE TABLE [dbo].[not_found_log] (
    [path] NVARCHAR(300) NOT NULL,
    [hits] INT NOT NULL CONSTRAINT [DF_not_found_log_hits] DEFAULT 1,
    [referrer] NVARCHAR(300) NULL,
    [first_seen] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_not_found_log_first_seen] DEFAULT SYSDATETIMEOFFSET(),
    [last_seen] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_not_found_log_last_seen] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_not_found_log] PRIMARY KEY CLUSTERED ([path]),
    CONSTRAINT [CK_not_found_log_path_check] CHECK (((REGEXP_LIKE([path], N'^/')) AND (LEN([path]) <= 300)))
);
GO

CREATE TABLE [dbo].[page_sections] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_page_sections_id] DEFAULT NEWSEQUENTIALID(),
    [page_id] UNIQUEIDENTIFIER NOT NULL,
    [type] NVARCHAR(MAX) NOT NULL,
    [data] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_page_sections_data] DEFAULT N'{}',
    [draft_data] NVARCHAR(MAX) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_page_sections_sort_order] DEFAULT 0,
    [is_visible] BIT NOT NULL CONSTRAINT [DF_page_sections_is_visible] DEFAULT 1,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_page_sections_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_page_sections_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_page_sections] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_page_sections_data_json] CHECK ([data] IS NULL OR ISJSON([data], VALUE) = 1),
    CONSTRAINT [CK_page_sections_draft_data_json] CHECK ([draft_data] IS NULL OR ISJSON([draft_data], VALUE) = 1),
    CONSTRAINT [CK_page_sections_data_check] CHECK ((ISJSON([data], OBJECT) = 1)),
    CONSTRAINT [CK_page_sections_draft_data_check] CHECK ((([draft_data] IS NULL) OR (ISJSON([draft_data], OBJECT) = 1))),
    CONSTRAINT [CK_page_sections_type_check] CHECK ((REGEXP_LIKE([type], N'^[a-z_]{1,40}$')))
);
GO

CREATE TABLE [dbo].[pages] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_pages_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [parent_id] UNIQUEIDENTIFIER NULL,
    [title] NVARCHAR(150) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [path] NVARCHAR(450) NOT NULL,
    [template] NVARCHAR(MAX) NOT NULL CONSTRAINT [DF_pages_template] DEFAULT N'default',
    [summary] NVARCHAR(300) NULL,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_pages_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_pages_sort_order] DEFAULT 0,
    [seo_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_pages_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_pages_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_pages] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_pages_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$')),
    CONSTRAINT [CK_pages_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_pages_path_check] CHECK ((REGEXP_LIKE([path], N'^/[a-z0-9/-]*$'))),
    CONSTRAINT [CK_pages_template_check] CHECK ((REGEXP_LIKE([template], N'^[a-z_]{1,40}$'))),
    CONSTRAINT [CK_pages_title_check] CHECK (LEN([title]) >= 1)
);
GO

CREATE TABLE [dbo].[permissions] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_permissions_id] DEFAULT NEWSEQUENTIALID(),
    [key] NVARCHAR(450) NOT NULL,
    [resource] AS CAST(LEFT([key], CHARINDEX(N'.', [key]) - 1) AS NVARCHAR(100)) PERSISTED,
    [action] AS CAST(SUBSTRING([key], CHARINDEX(N'.', [key]) + 1, 100) AS NVARCHAR(100)) PERSISTED,
    [description] NVARCHAR(MAX) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_permissions_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_permissions] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_permissions_key_check] CHECK ((REGEXP_LIKE([key], N'^[a-z_]+\.[a-z_]+$')))
);
GO

CREATE TABLE [dbo].[redirects] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_redirects_id] DEFAULT NEWSEQUENTIALID(),
    [from_path] NVARCHAR(450) NOT NULL,
    [to_path] NVARCHAR(MAX) NOT NULL,
    [status_code] SMALLINT NOT NULL CONSTRAINT [DF_redirects_status_code] DEFAULT 301,
    [hit_count] BIGINT NOT NULL CONSTRAINT [DF_redirects_hit_count] DEFAULT 0,
    [last_hit_at] DATETIMEOFFSET(3) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_redirects_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_redirects_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_redirects] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_redirects_from_path_check] CHECK ((REGEXP_LIKE([from_path], N'^/'))),
    CONSTRAINT [CK_redirects_not_self] CHECK (([from_path] <> [to_path])),
    CONSTRAINT [CK_redirects_status_code_check] CHECK (([status_code] IN (301, 302, 307, 308))),
    CONSTRAINT [CK_redirects_to_path_check] CHECK ((REGEXP_LIKE([to_path], N'^(/|https://)')))
);
GO

CREATE TABLE [dbo].[regions] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_regions_id] DEFAULT NEWSEQUENTIALID(),
    [name] NVARCHAR(100) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_regions_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_regions_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_regions_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_regions] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_regions_slug_format] CHECK (REGEXP_LIKE([slug], N'^[a-z0-9]+(-[a-z0-9]+)*$'))
);
GO

CREATE TABLE [dbo].[role_permissions] (
    [role_id] UNIQUEIDENTIFIER NOT NULL,
    [permission_id] UNIQUEIDENTIFIER NOT NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_role_permissions_created_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_role_permissions] PRIMARY KEY CLUSTERED ([role_id], [permission_id])
);
GO

CREATE TABLE [dbo].[roles] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_roles_id] DEFAULT NEWSEQUENTIALID(),
    [key] NVARCHAR(450) NOT NULL,
    [name] NVARCHAR(80) NOT NULL,
    [description] NVARCHAR(MAX) NULL,
    [is_system] BIT NOT NULL CONSTRAINT [DF_roles_is_system] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_roles_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_roles_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT [PK_roles] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_roles_key_check] CHECK ((REGEXP_LIKE([key], N'^[a-z][a-z0-9_]{1,49}$')))
);
GO

CREATE TABLE [dbo].[seo_metadata] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_seo_metadata_id] DEFAULT NEWSEQUENTIALID(),
    [route_path] NVARCHAR(450) NULL,
    [branch_id] UNIQUEIDENTIFIER NULL,
    [meta_title] NVARCHAR(70) NULL,
    [meta_description] NVARCHAR(170) NULL,
    [og_title] NVARCHAR(95) NULL,
    [og_description] NVARCHAR(200) NULL,
    [og_image_id] UNIQUEIDENTIFIER NULL,
    [canonical_url] NVARCHAR(MAX) NULL,
    [robots_noindex] BIT NOT NULL CONSTRAINT [DF_seo_metadata_robots_noindex] DEFAULT 0,
    [robots_nofollow] BIT NOT NULL CONSTRAINT [DF_seo_metadata_robots_nofollow] DEFAULT 0,
    [structured_data] NVARCHAR(MAX) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_seo_metadata_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_seo_metadata_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_seo_metadata] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_seo_metadata_structured_data_json] CHECK ([structured_data] IS NULL OR ISJSON([structured_data], VALUE) = 1),
    CONSTRAINT [CK_seo_metadata_canonical_url_check] CHECK ((REGEXP_LIKE([canonical_url], N'^https://'))),
    CONSTRAINT [CK_seo_metadata_route_path_check] CHECK ((REGEXP_LIKE([route_path], N'^/[a-z0-9/_-]*$'))),
    CONSTRAINT [CK_seo_metadata_structured_data_check] CHECK ((([structured_data] IS NULL) OR (ISJSON([structured_data], OBJECT) = 1)))
);
GO

CREATE TABLE [dbo].[site_settings] (
    [key] NVARCHAR(450) NOT NULL,
    [value] NVARCHAR(MAX) NOT NULL,
    [is_public] BIT NOT NULL CONSTRAINT [DF_site_settings_is_public] DEFAULT 0,
    [description] NVARCHAR(MAX) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_site_settings_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_site_settings_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_site_settings] PRIMARY KEY CLUSTERED ([key]),
    CONSTRAINT [CK_site_settings_value_json] CHECK ([value] IS NULL OR ISJSON([value], VALUE) = 1),
    CONSTRAINT [CK_site_settings_key_check] CHECK ((REGEXP_LIKE([key], N'^[a-z][a-z0-9_.]{1,80}$')))
);
GO

CREATE TABLE [dbo].[testimonials] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_testimonials_id] DEFAULT NEWSEQUENTIALID(),
    [branch_id] UNIQUEIDENTIFIER NULL,
    [author_name] NVARCHAR(120) NOT NULL,
    [author_role] NVARCHAR(120) NULL,
    [quote] NVARCHAR(600) NOT NULL,
    [photo_media_id] UNIQUEIDENTIFIER NULL,
    [is_featured] BIT NOT NULL CONSTRAINT [DF_testimonials_is_featured] DEFAULT 0,
    [status] NVARCHAR(13) NOT NULL CONSTRAINT [DF_testimonials_status] DEFAULT N'draft',
    [published_at] DATETIMEOFFSET(3) NULL,
    [sort_order] INT NOT NULL CONSTRAINT [DF_testimonials_sort_order] DEFAULT 0,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_testimonials_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_testimonials_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    [updated_by] UNIQUEIDENTIFIER NULL,
    [deleted_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_testimonials] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_testimonials_status_values] CHECK ([status] IN (N'draft', N'in_review', N'published', N'archived')),
    CONSTRAINT [CK_testimonials_quote_check] CHECK (LEN([quote]) >= 10)
);
GO

CREATE TABLE [dbo].[user_roles] (
    [id] UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_user_roles_id] DEFAULT NEWSEQUENTIALID(),
    [user_id] UNIQUEIDENTIFIER NOT NULL,
    [role_id] UNIQUEIDENTIFIER NOT NULL,
    [region_id] UNIQUEIDENTIFIER NULL,
    [branch_id] UNIQUEIDENTIFIER NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_user_roles_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [created_by] UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_user_roles] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_user_roles_single_scope] CHECK ((((CASE WHEN [region_id] IS NULL THEN 0 ELSE 1 END) + (CASE WHEN [branch_id] IS NULL THEN 0 ELSE 1 END)) <= 1))
);
GO

CREATE TABLE [dbo].[users] (
    [id] UNIQUEIDENTIFIER NOT NULL,
    [email] NVARCHAR(254) NOT NULL,
    [full_name] NVARCHAR(120) NOT NULL CONSTRAINT [DF_users_full_name] DEFAULT N'',
    [phone] NVARCHAR(20) NULL,
    [job_title] NVARCHAR(120) NULL,
    [avatar_media_id] UNIQUEIDENTIFIER NULL,
    [is_active] BIT NOT NULL CONSTRAINT [DF_users_is_active] DEFAULT 1,
    [last_seen_at] DATETIMEOFFSET(3) NULL,
    [created_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_users_created_at] DEFAULT SYSDATETIMEOFFSET(),
    [updated_at] DATETIMEOFFSET(3) NOT NULL CONSTRAINT [DF_users_updated_at] DEFAULT SYSDATETIMEOFFSET(),
    [deleted_at] DATETIMEOFFSET(3) NULL,
    [password_hash] NVARCHAR(400) NULL,
    [security_stamp] NVARCHAR(100) NULL,
    [email_confirmed] BIT NOT NULL CONSTRAINT [DF_users_email_confirmed] DEFAULT 0,
    [two_factor_enabled] BIT NOT NULL CONSTRAINT [DF_users_two_factor_enabled] DEFAULT 0,
    [authenticator_key] NVARCHAR(200) NULL,
    [lockout_end] DATETIMEOFFSET(3) NULL,
    [access_failed_count] INT NOT NULL CONSTRAINT [DF_users_access_failed_count] DEFAULT 0,
    [last_sign_in_at] DATETIMEOFFSET(3) NULL,
    CONSTRAINT [PK_users] PRIMARY KEY CLUSTERED ([id]),
    CONSTRAINT [CK_users_email_format] CHECK (REGEXP_LIKE([email], N'^[^@\s]+@[^@\s]+\.[^@\s]+$', 'i')),
    CONSTRAINT [CK_users_phone_format] CHECK (REGEXP_LIKE([phone], N'^\+?[0-9 ()-]{7,20}$'))
);
GO

-- Unique keys (nullable columns use filtered indexes, so several NULLs are allowed as in PostgreSQL) ----
CREATE UNIQUE NONCLUSTERED INDEX [UQ_achievements_seo_id_key] ON [dbo].[achievements] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_admission_documents_key] ON [dbo].[admission_documents] ([admission_id], [document_type], [media_id]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_admissions_reference_no_key] ON [dbo].[admissions] ([reference_no]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_branch_classes_key] ON [dbo].[branch_classes] ([branch_id], [class_level_id]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_branch_images_key] ON [dbo].[branch_images] ([branch_id], [media_id]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_branches_seo_key] ON [dbo].[branches] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_categories_type_slug_key] ON [dbo].[categories] ([type], [slug]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_cities_seo_key] ON [dbo].[cities] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_cities_slug_key] ON [dbo].[cities] ([slug]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_class_levels_name_key] ON [dbo].[class_levels] ([name]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_class_levels_slug_key] ON [dbo].[class_levels] ([slug]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_districts_slug_key] ON [dbo].[districts] ([slug]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_events_seo_id_key] ON [dbo].[events] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_facilities_name_key] ON [dbo].[facilities] ([name]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_facilities_slug_key] ON [dbo].[facilities] ([slug]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_franchise_applications_reference_no_key] ON [dbo].[franchise_applications] ([reference_no]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_gallery_albums_seo_id_key] ON [dbo].[gallery_albums] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_gallery_images_key] ON [dbo].[gallery_images] ([album_id], [media_id]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_head_offices_office_number_key] ON [dbo].[head_offices] ([office_number]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_homepage_content_section_key_key] ON [dbo].[homepage_content] ([section_key]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_job_postings_seo_id_key] ON [dbo].[job_postings] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_media_object_key] ON [dbo].[media] ([bucket], [storage_path]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_media_folders_name_key] ON [dbo].[media_folders] ([parent_id], [branch_id], [name]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_navigation_menus_key_key] ON [dbo].[navigation_menus] ([key]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_news_seo_id_key] ON [dbo].[news] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_pages_seo_id_key] ON [dbo].[pages] ([seo_id]) WHERE [seo_id] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_permissions_key_key] ON [dbo].[permissions] ([key]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_redirects_from_path_key] ON [dbo].[redirects] ([from_path]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_regions_slug_key] ON [dbo].[regions] ([slug]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_roles_key_key] ON [dbo].[roles] ([key]);
CREATE UNIQUE NONCLUSTERED INDEX [UQ_seo_metadata_route_path_key] ON [dbo].[seo_metadata] ([route_path]) WHERE [route_path] IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX [UQ_user_roles_unique] ON [dbo].[user_roles] ([user_id], [role_id], [region_id], [branch_id]);
GO

-- Indexes (PostgreSQL partial indexes become filtered indexes) ------------------------------
CREATE NONCLUSTERED INDEX [achievements_branch_idx] ON [dbo].[achievements] ([branch_id], [academic_year] DESC);
CREATE NONCLUSTERED INDEX [achievements_class_idx] ON [dbo].[achievements] ([class_level_id]);
CREATE NONCLUSTERED INDEX [achievements_created_by_idx] ON [dbo].[achievements] ([created_by]);
CREATE NONCLUSTERED INDEX [achievements_media_idx] ON [dbo].[achievements] ([media_id]);
CREATE NONCLUSTERED INDEX [achievements_public_idx] ON [dbo].[achievements] ([status], [type], [academic_year] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [achievements_seo_idx] ON [dbo].[achievements] ([seo_id]);
CREATE UNIQUE NONCLUSTERED INDEX [achievements_slug_key] ON [dbo].[achievements] ([slug]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [achievements_updated_by_idx] ON [dbo].[achievements] ([updated_by]);
CREATE NONCLUSTERED INDEX [admission_cycles_created_by_idx] ON [dbo].[admission_cycles] ([created_by]);
CREATE UNIQUE NONCLUSTERED INDEX [admission_cycles_one_active] ON [dbo].[admission_cycles] ([is_active]) WHERE [is_active] = 1;
CREATE NONCLUSTERED INDEX [admission_cycles_updated_by_idx] ON [dbo].[admission_cycles] ([updated_by]);
CREATE NONCLUSTERED INDEX [admission_documents_admission_idx] ON [dbo].[admission_documents] ([admission_id]);
CREATE NONCLUSTERED INDEX [admission_documents_media_idx] ON [dbo].[admission_documents] ([media_id]);
CREATE NONCLUSTERED INDEX [admission_documents_verified_by_idx] ON [dbo].[admission_documents] ([verified_by]);
CREATE NONCLUSTERED INDEX [admission_inquiries_assigned_idx] ON [dbo].[admission_inquiries] ([assigned_to]) WHERE [assigned_to] IS NOT NULL;
CREATE NONCLUSTERED INDEX [admission_inquiries_class_idx] ON [dbo].[admission_inquiries] ([class_level_id]);
CREATE NONCLUSTERED INDEX [admission_inquiries_converted_idx] ON [dbo].[admission_inquiries] ([converted_admission_id]);
CREATE NONCLUSTERED INDEX [admission_inquiries_inbox_idx] ON [dbo].[admission_inquiries] ([branch_id], [status], [created_at] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [admission_inquiries_updated_by_idx] ON [dbo].[admission_inquiries] ([updated_by]);
CREATE NONCLUSTERED INDEX [admission_notes_admission_idx] ON [dbo].[admission_notes] ([admission_id], [created_at]);
CREATE NONCLUSTERED INDEX [admission_notes_author_idx] ON [dbo].[admission_notes] ([author_id]);
CREATE UNIQUE NONCLUSTERED INDEX [admission_stages_one_initial] ON [dbo].[admission_stages] ([is_initial]) WHERE [is_initial] = 1;
CREATE NONCLUSTERED INDEX [admission_status_history_admission_idx] ON [dbo].[admission_status_history] ([admission_id], [created_at]);
CREATE NONCLUSTERED INDEX [admission_status_history_changed_by_idx] ON [dbo].[admission_status_history] ([changed_by]);
CREATE NONCLUSTERED INDEX [admission_status_history_from_stage_idx] ON [dbo].[admission_status_history] ([from_stage]);
CREATE NONCLUSTERED INDEX [admission_status_history_to_stage_idx] ON [dbo].[admission_status_history] ([to_stage]);
CREATE NONCLUSTERED INDEX [admissions_assigned_idx] ON [dbo].[admissions] ([assigned_to]) WHERE [assigned_to] IS NOT NULL;
CREATE NONCLUSTERED INDEX [admissions_class_idx] ON [dbo].[admissions] ([class_level_id]);
CREATE NONCLUSTERED INDEX [admissions_created_by_idx] ON [dbo].[admissions] ([created_by]);
CREATE NONCLUSTERED INDEX [admissions_cycle_idx] ON [dbo].[admissions] ([cycle_id]);
CREATE NONCLUSTERED INDEX [admissions_guardian_phone_idx] ON [dbo].[admissions] ([guardian_phone]);
CREATE NONCLUSTERED INDEX [admissions_inquiry_idx] ON [dbo].[admissions] ([inquiry_id]);
CREATE NONCLUSTERED INDEX [admissions_pipeline_idx] ON [dbo].[admissions] ([branch_id], [stage_key], [submitted_at] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [admissions_retention_idx] ON [dbo].[admissions] ([retention_until]) WHERE [retention_until] IS NOT NULL;
CREATE NONCLUSTERED INDEX [admissions_stage_idx] ON [dbo].[admissions] ([stage_key]);
CREATE NONCLUSTERED INDEX [admissions_submitted_idx] ON [dbo].[admissions] ([submitted_at] DESC);
CREATE NONCLUSTERED INDEX [admissions_updated_by_idx] ON [dbo].[admissions] ([updated_by]);
CREATE NONCLUSTERED INDEX [audit_logs_actor_idx] ON [dbo].[audit_logs] ([actor_id], [created_at] DESC);
CREATE NONCLUSTERED INDEX [audit_logs_record_idx] ON [dbo].[audit_logs] ([table_name], [record_id], [created_at] DESC);
CREATE NONCLUSTERED INDEX [branch_classes_created_by_idx] ON [dbo].[branch_classes] ([created_by]);
CREATE NONCLUSTERED INDEX [branch_classes_level_idx] ON [dbo].[branch_classes] ([class_level_id]);
CREATE NONCLUSTERED INDEX [branch_classes_open_idx] ON [dbo].[branch_classes] ([branch_id]) WHERE [is_admission_open] = 1;
CREATE NONCLUSTERED INDEX [branch_classes_updated_by_idx] ON [dbo].[branch_classes] ([updated_by]);
CREATE NONCLUSTERED INDEX [branch_facilities_facility_idx] ON [dbo].[branch_facilities] ([facility_id]);
CREATE NONCLUSTERED INDEX [branch_images_branch_idx] ON [dbo].[branch_images] ([branch_id], [sort_order]);
CREATE NONCLUSTERED INDEX [branch_images_created_by_idx] ON [dbo].[branch_images] ([created_by]);
CREATE NONCLUSTERED INDEX [branch_images_media_idx] ON [dbo].[branch_images] ([media_id]);
CREATE NONCLUSTERED INDEX [branch_images_updated_by_idx] ON [dbo].[branch_images] ([updated_by]);
CREATE NONCLUSTERED INDEX [branches_admission_open_idx] ON [dbo].[branches] ([is_admission_open]) WHERE [deleted_at] IS NULL AND [is_admission_open] = 1;
CREATE NONCLUSTERED INDEX [branches_admission_status_idx] ON [dbo].[branches] ([admission_status]) WHERE [archived_at] IS NULL AND [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [branches_archived_idx] ON [dbo].[branches] ([archived_at]) WHERE [archived_at] IS NOT NULL;
CREATE NONCLUSTERED INDEX [branches_city_idx] ON [dbo].[branches] ([city_id]);
CREATE UNIQUE NONCLUSTERED INDEX [branches_city_slug_key] ON [dbo].[branches] ([city_id], [slug]) WHERE [deleted_at] IS NULL;
CREATE UNIQUE NONCLUSTERED INDEX [branches_code_key] ON [dbo].[branches] ([code]) WHERE [code] IS NOT NULL AND [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [branches_cover_media_idx] ON [dbo].[branches] ([cover_media_id]);
CREATE NONCLUSTERED INDEX [branches_created_by_idx] ON [dbo].[branches] ([created_by]);
CREATE NONCLUSTERED INDEX [branches_geo_idx] ON [dbo].[branches] ([latitude], [longitude]) WHERE [latitude] IS NOT NULL;
CREATE NONCLUSTERED INDEX [branches_principal_media_idx] ON [dbo].[branches] ([principal_media_id]);
CREATE NONCLUSTERED INDEX [branches_public_idx] ON [dbo].[branches] ([city_id], [area_slug]) WHERE [is_published] = 1 AND [archived_at] IS NULL AND [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [branches_seo_idx] ON [dbo].[branches] ([seo_id]);
CREATE NONCLUSTERED INDEX [branches_status_idx] ON [dbo].[branches] ([status]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [branches_updated_by_idx] ON [dbo].[branches] ([updated_by]);
CREATE NONCLUSTERED INDEX [categories_created_by_idx] ON [dbo].[categories] ([created_by]);
CREATE NONCLUSTERED INDEX [categories_updated_by_idx] ON [dbo].[categories] ([updated_by]);
CREATE NONCLUSTERED INDEX [cities_created_by_idx] ON [dbo].[cities] ([created_by]);
CREATE NONCLUSTERED INDEX [cities_district_idx] ON [dbo].[cities] ([district_id]);
CREATE NONCLUSTERED INDEX [cities_region_idx] ON [dbo].[cities] ([region_id]);
CREATE NONCLUSTERED INDEX [cities_seo_idx] ON [dbo].[cities] ([seo_id]);
CREATE NONCLUSTERED INDEX [cities_updated_by_idx] ON [dbo].[cities] ([updated_by]);
CREATE NONCLUSTERED INDEX [class_levels_created_by_idx] ON [dbo].[class_levels] ([created_by]);
CREATE NONCLUSTERED INDEX [class_levels_stage_idx] ON [dbo].[class_levels] ([stage], [sort_order]);
CREATE NONCLUSTERED INDEX [class_levels_updated_by_idx] ON [dbo].[class_levels] ([updated_by]);
CREATE NONCLUSTERED INDEX [contact_messages_assigned_idx] ON [dbo].[contact_messages] ([assigned_to]) WHERE [assigned_to] IS NOT NULL;
CREATE NONCLUSTERED INDEX [contact_messages_inbox_idx] ON [dbo].[contact_messages] ([branch_id], [status], [created_at] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [contact_messages_updated_by_idx] ON [dbo].[contact_messages] ([updated_by]);
CREATE NONCLUSTERED INDEX [districts_created_by_idx] ON [dbo].[districts] ([created_by]);
CREATE NONCLUSTERED INDEX [districts_region_idx] ON [dbo].[districts] ([region_id]);
CREATE NONCLUSTERED INDEX [districts_updated_by_idx] ON [dbo].[districts] ([updated_by]);
CREATE NONCLUSTERED INDEX [downloads_branch_idx] ON [dbo].[downloads] ([branch_id]);
CREATE NONCLUSTERED INDEX [downloads_category_idx] ON [dbo].[downloads] ([category_id]);
CREATE NONCLUSTERED INDEX [downloads_created_by_idx] ON [dbo].[downloads] ([created_by]);
CREATE NONCLUSTERED INDEX [downloads_media_idx] ON [dbo].[downloads] ([media_id]);
CREATE NONCLUSTERED INDEX [downloads_public_idx] ON [dbo].[downloads] ([status], [published_at] DESC) WHERE [deleted_at] IS NULL;
CREATE UNIQUE NONCLUSTERED INDEX [downloads_slug_key] ON [dbo].[downloads] ([slug]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [downloads_updated_by_idx] ON [dbo].[downloads] ([updated_by]);
CREATE NONCLUSTERED INDEX [events_branch_idx] ON [dbo].[events] ([branch_id], [starts_at]);
CREATE NONCLUSTERED INDEX [events_category_idx] ON [dbo].[events] ([category_id]);
CREATE NONCLUSTERED INDEX [events_cover_idx] ON [dbo].[events] ([cover_media_id]);
CREATE NONCLUSTERED INDEX [events_created_by_idx] ON [dbo].[events] ([created_by]);
CREATE NONCLUSTERED INDEX [events_seo_idx] ON [dbo].[events] ([seo_id]);
CREATE UNIQUE NONCLUSTERED INDEX [events_slug_key] ON [dbo].[events] ([slug]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [events_upcoming_idx] ON [dbo].[events] ([starts_at]) WHERE [deleted_at] IS NULL AND [status] = N'published';
CREATE NONCLUSTERED INDEX [events_updated_by_idx] ON [dbo].[events] ([updated_by]);
CREATE NONCLUSTERED INDEX [facilities_created_by_idx] ON [dbo].[facilities] ([created_by]);
CREATE NONCLUSTERED INDEX [facilities_updated_by_idx] ON [dbo].[facilities] ([updated_by]);
CREATE NONCLUSTERED INDEX [franchise_applications_assigned_idx] ON [dbo].[franchise_applications] ([assigned_to]) WHERE [assigned_to] IS NOT NULL;
CREATE NONCLUSTERED INDEX [franchise_applications_email_idx] ON [dbo].[franchise_applications] ([email]);
CREATE NONCLUSTERED INDEX [franchise_applications_list_idx] ON [dbo].[franchise_applications] ([status], [submitted_at] DESC);
CREATE NONCLUSTERED INDEX [franchise_applications_mobile_idx] ON [dbo].[franchise_applications] ([mobile]);
CREATE NONCLUSTERED INDEX [franchise_applications_submitted_idx] ON [dbo].[franchise_applications] ([submitted_at] DESC);
CREATE NONCLUSTERED INDEX [franchise_applications_updated_by_idx] ON [dbo].[franchise_applications] ([updated_by]);
CREATE NONCLUSTERED INDEX [franchise_notes_app_idx] ON [dbo].[franchise_notes] ([application_id], [created_at]);
CREATE NONCLUSTERED INDEX [franchise_notes_author_idx] ON [dbo].[franchise_notes] ([author_id]);
CREATE NONCLUSTERED INDEX [franchise_settings_updated_by_idx] ON [dbo].[franchise_settings] ([updated_by]);
CREATE NONCLUSTERED INDEX [franchise_status_history_app_idx] ON [dbo].[franchise_status_history] ([application_id], [created_at]);
CREATE NONCLUSTERED INDEX [franchise_status_history_changed_by_idx] ON [dbo].[franchise_status_history] ([changed_by]);
CREATE NONCLUSTERED INDEX [gallery_albums_branch_idx] ON [dbo].[gallery_albums] ([branch_id], [album_date] DESC);
CREATE NONCLUSTERED INDEX [gallery_albums_category_idx] ON [dbo].[gallery_albums] ([category_id]);
CREATE NONCLUSTERED INDEX [gallery_albums_cover_idx] ON [dbo].[gallery_albums] ([cover_media_id]);
CREATE NONCLUSTERED INDEX [gallery_albums_created_by_idx] ON [dbo].[gallery_albums] ([created_by]);
CREATE NONCLUSTERED INDEX [gallery_albums_event_idx] ON [dbo].[gallery_albums] ([event_id]);
CREATE NONCLUSTERED INDEX [gallery_albums_public_idx] ON [dbo].[gallery_albums] ([status], [album_date] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [gallery_albums_seo_idx] ON [dbo].[gallery_albums] ([seo_id]);
CREATE UNIQUE NONCLUSTERED INDEX [gallery_albums_slug_key] ON [dbo].[gallery_albums] ([slug]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [gallery_albums_updated_by_idx] ON [dbo].[gallery_albums] ([updated_by]);
CREATE NONCLUSTERED INDEX [gallery_images_album_idx] ON [dbo].[gallery_images] ([album_id], [sort_order]);
CREATE NONCLUSTERED INDEX [gallery_images_created_by_idx] ON [dbo].[gallery_images] ([created_by]);
CREATE NONCLUSTERED INDEX [gallery_images_media_idx] ON [dbo].[gallery_images] ([media_id]);
CREATE NONCLUSTERED INDEX [gallery_images_updated_by_idx] ON [dbo].[gallery_images] ([updated_by]);
CREATE NONCLUSTERED INDEX [head_offices_created_by_idx] ON [dbo].[head_offices] ([created_by]);
CREATE NONCLUSTERED INDEX [head_offices_updated_by_idx] ON [dbo].[head_offices] ([updated_by]);
CREATE NONCLUSTERED INDEX [homepage_content_created_by_idx] ON [dbo].[homepage_content] ([created_by]);
CREATE NONCLUSTERED INDEX [homepage_content_order_idx] ON [dbo].[homepage_content] ([sort_order]) WHERE [is_visible] = 1;
CREATE NONCLUSTERED INDEX [homepage_content_updated_by_idx] ON [dbo].[homepage_content] ([updated_by]);
CREATE NONCLUSTERED INDEX [job_application_history_app_idx] ON [dbo].[job_application_history] ([job_application_id], [created_at]);
CREATE NONCLUSTERED INDEX [job_application_history_changed_by_idx] ON [dbo].[job_application_history] ([changed_by]);
CREATE NONCLUSTERED INDEX [job_applications_assigned_idx] ON [dbo].[job_applications] ([assigned_to]) WHERE [assigned_to] IS NOT NULL;
CREATE NONCLUSTERED INDEX [job_applications_branch_idx] ON [dbo].[job_applications] ([branch_id], [status]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [job_applications_cv_idx] ON [dbo].[job_applications] ([cv_media_id]);
CREATE NONCLUSTERED INDEX [job_applications_email_idx] ON [dbo].[job_applications] ([email]);
CREATE NONCLUSTERED INDEX [job_applications_pipeline_idx] ON [dbo].[job_applications] ([job_posting_id], [status], [created_at] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [job_applications_retention_idx] ON [dbo].[job_applications] ([retention_until]) WHERE [retention_until] IS NOT NULL;
CREATE NONCLUSTERED INDEX [job_applications_updated_by_idx] ON [dbo].[job_applications] ([updated_by]);
CREATE NONCLUSTERED INDEX [job_postings_branch_idx] ON [dbo].[job_postings] ([branch_id]);
CREATE NONCLUSTERED INDEX [job_postings_created_by_idx] ON [dbo].[job_postings] ([created_by]);
CREATE NONCLUSTERED INDEX [job_postings_public_idx] ON [dbo].[job_postings] ([status], [closes_on]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [job_postings_seo_idx] ON [dbo].[job_postings] ([seo_id]);
CREATE UNIQUE NONCLUSTERED INDEX [job_postings_slug_key] ON [dbo].[job_postings] ([slug]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [job_postings_updated_by_idx] ON [dbo].[job_postings] ([updated_by]);
CREATE NONCLUSTERED INDEX [media_branch_idx] ON [dbo].[media] ([branch_id]);
CREATE NONCLUSTERED INDEX [media_checksum_idx] ON [dbo].[media] ([checksum_sha256]) WHERE [checksum_sha256] IS NOT NULL;
CREATE NONCLUSTERED INDEX [media_created_by_idx] ON [dbo].[media] ([created_by]);
CREATE NONCLUSTERED INDEX [media_created_idx] ON [dbo].[media] ([created_at] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [media_folder_idx] ON [dbo].[media] ([folder_id]);
CREATE NONCLUSTERED INDEX [media_mime_idx] ON [dbo].[media] ([mime_type]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [media_updated_by_idx] ON [dbo].[media] ([updated_by]);
CREATE NONCLUSTERED INDEX [media_folders_branch_idx] ON [dbo].[media_folders] ([branch_id]);
CREATE NONCLUSTERED INDEX [media_folders_created_by_idx] ON [dbo].[media_folders] ([created_by]);
CREATE NONCLUSTERED INDEX [media_folders_parent_idx] ON [dbo].[media_folders] ([parent_id]);
CREATE NONCLUSTERED INDEX [media_folders_updated_by_idx] ON [dbo].[media_folders] ([updated_by]);
CREATE NONCLUSTERED INDEX [navigation_items_created_by_idx] ON [dbo].[navigation_items] ([created_by]);
CREATE NONCLUSTERED INDEX [navigation_items_menu_idx] ON [dbo].[navigation_items] ([menu_id], [parent_id], [sort_order]);
CREATE NONCLUSTERED INDEX [navigation_items_page_idx] ON [dbo].[navigation_items] ([page_id]);
CREATE NONCLUSTERED INDEX [navigation_items_parent_idx] ON [dbo].[navigation_items] ([parent_id]);
CREATE NONCLUSTERED INDEX [navigation_items_updated_by_idx] ON [dbo].[navigation_items] ([updated_by]);
CREATE NONCLUSTERED INDEX [navigation_menus_created_by_idx] ON [dbo].[navigation_menus] ([created_by]);
CREATE NONCLUSTERED INDEX [navigation_menus_updated_by_idx] ON [dbo].[navigation_menus] ([updated_by]);
CREATE NONCLUSTERED INDEX [news_author_idx] ON [dbo].[news] ([author_id]);
CREATE NONCLUSTERED INDEX [news_branch_idx] ON [dbo].[news] ([branch_id], [published_at] DESC);
CREATE NONCLUSTERED INDEX [news_category_idx] ON [dbo].[news] ([category_id]);
CREATE NONCLUSTERED INDEX [news_cover_idx] ON [dbo].[news] ([cover_media_id]);
CREATE NONCLUSTERED INDEX [news_created_by_idx] ON [dbo].[news] ([created_by]);
CREATE NONCLUSTERED INDEX [news_featured_idx] ON [dbo].[news] ([published_at] DESC) WHERE [is_featured] = 1 AND [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [news_public_idx] ON [dbo].[news] ([status], [published_at] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [news_seo_idx] ON [dbo].[news] ([seo_id]);
CREATE UNIQUE NONCLUSTERED INDEX [news_slug_key] ON [dbo].[news] ([slug]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [news_updated_by_idx] ON [dbo].[news] ([updated_by]);
CREATE NONCLUSTERED INDEX [not_found_log_hits_idx] ON [dbo].[not_found_log] ([hits] DESC, [last_seen] DESC);
CREATE NONCLUSTERED INDEX [page_sections_created_by_idx] ON [dbo].[page_sections] ([created_by]);
CREATE NONCLUSTERED INDEX [page_sections_page_idx] ON [dbo].[page_sections] ([page_id], [sort_order]);
CREATE NONCLUSTERED INDEX [page_sections_updated_by_idx] ON [dbo].[page_sections] ([updated_by]);
CREATE NONCLUSTERED INDEX [pages_branch_idx] ON [dbo].[pages] ([branch_id]);
CREATE NONCLUSTERED INDEX [pages_created_by_idx] ON [dbo].[pages] ([created_by]);
CREATE NONCLUSTERED INDEX [pages_parent_idx] ON [dbo].[pages] ([parent_id]);
CREATE UNIQUE NONCLUSTERED INDEX [pages_path_key] ON [dbo].[pages] ([path]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [pages_public_idx] ON [dbo].[pages] ([status], [published_at] DESC) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [pages_seo_idx] ON [dbo].[pages] ([seo_id]);
CREATE NONCLUSTERED INDEX [pages_updated_by_idx] ON [dbo].[pages] ([updated_by]);
CREATE NONCLUSTERED INDEX [permissions_resource_idx] ON [dbo].[permissions] ([resource]);
CREATE NONCLUSTERED INDEX [redirects_created_by_idx] ON [dbo].[redirects] ([created_by]);
CREATE NONCLUSTERED INDEX [redirects_updated_by_idx] ON [dbo].[redirects] ([updated_by]);
CREATE NONCLUSTERED INDEX [regions_created_by_idx] ON [dbo].[regions] ([created_by]);
CREATE NONCLUSTERED INDEX [regions_updated_by_idx] ON [dbo].[regions] ([updated_by]);
CREATE NONCLUSTERED INDEX [role_permissions_permission_idx] ON [dbo].[role_permissions] ([permission_id]);
CREATE NONCLUSTERED INDEX [seo_metadata_branch_idx] ON [dbo].[seo_metadata] ([branch_id]);
CREATE NONCLUSTERED INDEX [seo_metadata_created_by_idx] ON [dbo].[seo_metadata] ([created_by]);
CREATE NONCLUSTERED INDEX [seo_metadata_og_image_idx] ON [dbo].[seo_metadata] ([og_image_id]);
CREATE NONCLUSTERED INDEX [seo_metadata_updated_by_idx] ON [dbo].[seo_metadata] ([updated_by]);
CREATE NONCLUSTERED INDEX [site_settings_created_by_idx] ON [dbo].[site_settings] ([created_by]);
CREATE NONCLUSTERED INDEX [site_settings_updated_by_idx] ON [dbo].[site_settings] ([updated_by]);
CREATE NONCLUSTERED INDEX [testimonials_branch_idx] ON [dbo].[testimonials] ([branch_id]);
CREATE NONCLUSTERED INDEX [testimonials_created_by_idx] ON [dbo].[testimonials] ([created_by]);
CREATE NONCLUSTERED INDEX [testimonials_photo_idx] ON [dbo].[testimonials] ([photo_media_id]);
CREATE NONCLUSTERED INDEX [testimonials_public_idx] ON [dbo].[testimonials] ([status], [is_featured], [sort_order]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [testimonials_updated_by_idx] ON [dbo].[testimonials] ([updated_by]);
CREATE NONCLUSTERED INDEX [user_roles_branch_idx] ON [dbo].[user_roles] ([branch_id]) WHERE [branch_id] IS NOT NULL;
CREATE NONCLUSTERED INDEX [user_roles_created_by_idx] ON [dbo].[user_roles] ([created_by]);
CREATE NONCLUSTERED INDEX [user_roles_region_idx] ON [dbo].[user_roles] ([region_id]) WHERE [region_id] IS NOT NULL;
CREATE NONCLUSTERED INDEX [user_roles_role_idx] ON [dbo].[user_roles] ([role_id]);
CREATE NONCLUSTERED INDEX [user_roles_user_idx] ON [dbo].[user_roles] ([user_id]);
CREATE NONCLUSTERED INDEX [users_active_idx] ON [dbo].[users] ([is_active]) WHERE [deleted_at] IS NULL;
CREATE NONCLUSTERED INDEX [users_avatar_idx] ON [dbo].[users] ([avatar_media_id]);
CREATE UNIQUE NONCLUSTERED INDEX [users_email_key] ON [dbo].[users] ([email]);
GO

-- Foreign keys ----------------------------------------------------------------------------
ALTER TABLE [dbo].[achievements] ADD CONSTRAINT [FK_achievements_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[achievements] ADD CONSTRAINT [FK_achievements_class_level_id_fkey] FOREIGN KEY ([class_level_id]) REFERENCES [dbo].[class_levels] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[achievements] ADD CONSTRAINT [FK_achievements_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[achievements] ADD CONSTRAINT [FK_achievements_media_id_fkey] FOREIGN KEY ([media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[achievements] ADD CONSTRAINT [FK_achievements_seo_id_fkey] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[achievements] ADD CONSTRAINT [FK_achievements_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_cycles] ADD CONSTRAINT [FK_admission_cycles_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_cycles] ADD CONSTRAINT [FK_admission_cycles_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_documents] ADD CONSTRAINT [FK_admission_documents_admission_id_fkey] FOREIGN KEY ([admission_id]) REFERENCES [dbo].[admissions] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[admission_documents] ADD CONSTRAINT [FK_admission_documents_media_id_fkey] FOREIGN KEY ([media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_documents] ADD CONSTRAINT [FK_admission_documents_verified_by_fkey] FOREIGN KEY ([verified_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_inquiries] ADD CONSTRAINT [FK_admission_inquiries_assigned_to_fkey] FOREIGN KEY ([assigned_to]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_inquiries] ADD CONSTRAINT [FK_admission_inquiries_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_inquiries] ADD CONSTRAINT [FK_admission_inquiries_class_level_id_fkey] FOREIGN KEY ([class_level_id]) REFERENCES [dbo].[class_levels] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_inquiries] ADD CONSTRAINT [FK_admission_inquiries_converted_admission_id_fkey] FOREIGN KEY ([converted_admission_id]) REFERENCES [dbo].[admissions] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_inquiries] ADD CONSTRAINT [FK_admission_inquiries_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_notes] ADD CONSTRAINT [FK_admission_notes_admission_id_fkey] FOREIGN KEY ([admission_id]) REFERENCES [dbo].[admissions] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[admission_notes] ADD CONSTRAINT [FK_admission_notes_author_id_fkey] FOREIGN KEY ([author_id]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_status_history] ADD CONSTRAINT [FK_admission_status_history_admission_id_fkey] FOREIGN KEY ([admission_id]) REFERENCES [dbo].[admissions] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[admission_status_history] ADD CONSTRAINT [FK_admission_status_history_changed_by_fkey] FOREIGN KEY ([changed_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_status_history] ADD CONSTRAINT [FK_admission_status_history_from_stage_fkey] FOREIGN KEY ([from_stage]) REFERENCES [dbo].[admission_stages] ([key]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admission_status_history] ADD CONSTRAINT [FK_admission_status_history_to_stage_fkey] FOREIGN KEY ([to_stage]) REFERENCES [dbo].[admission_stages] ([key]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_assigned_to_fkey] FOREIGN KEY ([assigned_to]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_branch_class_fk] FOREIGN KEY ([branch_id], [class_level_id]) REFERENCES [dbo].[branch_classes] ([branch_id], [class_level_id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_class_level_id_fkey] FOREIGN KEY ([class_level_id]) REFERENCES [dbo].[class_levels] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_cycle_id_fkey] FOREIGN KEY ([cycle_id]) REFERENCES [dbo].[admission_cycles] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_inquiry_fk] FOREIGN KEY ([inquiry_id]) REFERENCES [dbo].[admission_inquiries] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_stage_key_fkey] FOREIGN KEY ([stage_key]) REFERENCES [dbo].[admission_stages] ([key]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[admissions] ADD CONSTRAINT [FK_admissions_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[audit_logs] ADD CONSTRAINT [FK_audit_logs_actor_id_fkey] FOREIGN KEY ([actor_id]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branch_classes] ADD CONSTRAINT [FK_branch_classes_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[branch_classes] ADD CONSTRAINT [FK_branch_classes_class_level_id_fkey] FOREIGN KEY ([class_level_id]) REFERENCES [dbo].[class_levels] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branch_classes] ADD CONSTRAINT [FK_branch_classes_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branch_classes] ADD CONSTRAINT [FK_branch_classes_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branch_facilities] ADD CONSTRAINT [FK_branch_facilities_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[branch_facilities] ADD CONSTRAINT [FK_branch_facilities_facility_id_fkey] FOREIGN KEY ([facility_id]) REFERENCES [dbo].[facilities] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branch_images] ADD CONSTRAINT [FK_branch_images_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[branch_images] ADD CONSTRAINT [FK_branch_images_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branch_images] ADD CONSTRAINT [FK_branch_images_media_id_fkey] FOREIGN KEY ([media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[branch_images] ADD CONSTRAINT [FK_branch_images_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branches] ADD CONSTRAINT [FK_branches_city_id_fkey] FOREIGN KEY ([city_id]) REFERENCES [dbo].[cities] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branches] ADD CONSTRAINT [FK_branches_cover_media_fk] FOREIGN KEY ([cover_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branches] ADD CONSTRAINT [FK_branches_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branches] ADD CONSTRAINT [FK_branches_principal_media_fk] FOREIGN KEY ([principal_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branches] ADD CONSTRAINT [FK_branches_seo_fk] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[branches] ADD CONSTRAINT [FK_branches_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[categories] ADD CONSTRAINT [FK_categories_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[categories] ADD CONSTRAINT [FK_categories_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[cities] ADD CONSTRAINT [FK_cities_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[cities] ADD CONSTRAINT [FK_cities_district_id_fkey] FOREIGN KEY ([district_id]) REFERENCES [dbo].[districts] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[cities] ADD CONSTRAINT [FK_cities_region_id_fkey] FOREIGN KEY ([region_id]) REFERENCES [dbo].[regions] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[cities] ADD CONSTRAINT [FK_cities_seo_fk] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[cities] ADD CONSTRAINT [FK_cities_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[class_levels] ADD CONSTRAINT [FK_class_levels_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[class_levels] ADD CONSTRAINT [FK_class_levels_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[contact_messages] ADD CONSTRAINT [FK_contact_messages_assigned_to_fkey] FOREIGN KEY ([assigned_to]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[contact_messages] ADD CONSTRAINT [FK_contact_messages_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[contact_messages] ADD CONSTRAINT [FK_contact_messages_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[districts] ADD CONSTRAINT [FK_districts_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[districts] ADD CONSTRAINT [FK_districts_region_id_fkey] FOREIGN KEY ([region_id]) REFERENCES [dbo].[regions] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[districts] ADD CONSTRAINT [FK_districts_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[download_stats] ADD CONSTRAINT [FK_download_stats_download_id_fkey] FOREIGN KEY ([download_id]) REFERENCES [dbo].[downloads] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[downloads] ADD CONSTRAINT [FK_downloads_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[downloads] ADD CONSTRAINT [FK_downloads_category_id_fkey] FOREIGN KEY ([category_id]) REFERENCES [dbo].[categories] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[downloads] ADD CONSTRAINT [FK_downloads_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[downloads] ADD CONSTRAINT [FK_downloads_media_id_fkey] FOREIGN KEY ([media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[downloads] ADD CONSTRAINT [FK_downloads_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[events] ADD CONSTRAINT [FK_events_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[events] ADD CONSTRAINT [FK_events_category_id_fkey] FOREIGN KEY ([category_id]) REFERENCES [dbo].[categories] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[events] ADD CONSTRAINT [FK_events_cover_media_id_fkey] FOREIGN KEY ([cover_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[events] ADD CONSTRAINT [FK_events_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[events] ADD CONSTRAINT [FK_events_seo_id_fkey] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[events] ADD CONSTRAINT [FK_events_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[facilities] ADD CONSTRAINT [FK_facilities_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[facilities] ADD CONSTRAINT [FK_facilities_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[franchise_applications] ADD CONSTRAINT [FK_franchise_applications_assigned_to_fkey] FOREIGN KEY ([assigned_to]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[franchise_applications] ADD CONSTRAINT [FK_franchise_applications_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[franchise_notes] ADD CONSTRAINT [FK_franchise_notes_application_id_fkey] FOREIGN KEY ([application_id]) REFERENCES [dbo].[franchise_applications] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[franchise_notes] ADD CONSTRAINT [FK_franchise_notes_author_id_fkey] FOREIGN KEY ([author_id]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[franchise_settings] ADD CONSTRAINT [FK_franchise_settings_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[franchise_status_history] ADD CONSTRAINT [FK_franchise_status_history_application_id_fkey] FOREIGN KEY ([application_id]) REFERENCES [dbo].[franchise_applications] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[franchise_status_history] ADD CONSTRAINT [FK_franchise_status_history_changed_by_fkey] FOREIGN KEY ([changed_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_albums] ADD CONSTRAINT [FK_gallery_albums_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[gallery_albums] ADD CONSTRAINT [FK_gallery_albums_category_id_fkey] FOREIGN KEY ([category_id]) REFERENCES [dbo].[categories] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_albums] ADD CONSTRAINT [FK_gallery_albums_cover_media_id_fkey] FOREIGN KEY ([cover_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_albums] ADD CONSTRAINT [FK_gallery_albums_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_albums] ADD CONSTRAINT [FK_gallery_albums_event_id_fkey] FOREIGN KEY ([event_id]) REFERENCES [dbo].[events] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_albums] ADD CONSTRAINT [FK_gallery_albums_seo_id_fkey] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_albums] ADD CONSTRAINT [FK_gallery_albums_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_images] ADD CONSTRAINT [FK_gallery_images_album_id_fkey] FOREIGN KEY ([album_id]) REFERENCES [dbo].[gallery_albums] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[gallery_images] ADD CONSTRAINT [FK_gallery_images_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[gallery_images] ADD CONSTRAINT [FK_gallery_images_media_id_fkey] FOREIGN KEY ([media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[gallery_images] ADD CONSTRAINT [FK_gallery_images_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[head_offices] ADD CONSTRAINT [FK_head_offices_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[head_offices] ADD CONSTRAINT [FK_head_offices_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[homepage_content] ADD CONSTRAINT [FK_homepage_content_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[homepage_content] ADD CONSTRAINT [FK_homepage_content_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_application_history] ADD CONSTRAINT [FK_job_application_history_changed_by_fkey] FOREIGN KEY ([changed_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_application_history] ADD CONSTRAINT [FK_job_application_history_job_application_id_fkey] FOREIGN KEY ([job_application_id]) REFERENCES [dbo].[job_applications] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[job_applications] ADD CONSTRAINT [FK_job_applications_assigned_to_fkey] FOREIGN KEY ([assigned_to]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_applications] ADD CONSTRAINT [FK_job_applications_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_applications] ADD CONSTRAINT [FK_job_applications_cv_media_id_fkey] FOREIGN KEY ([cv_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_applications] ADD CONSTRAINT [FK_job_applications_job_posting_id_fkey] FOREIGN KEY ([job_posting_id]) REFERENCES [dbo].[job_postings] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_applications] ADD CONSTRAINT [FK_job_applications_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_postings] ADD CONSTRAINT [FK_job_postings_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[job_postings] ADD CONSTRAINT [FK_job_postings_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_postings] ADD CONSTRAINT [FK_job_postings_seo_id_fkey] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[job_postings] ADD CONSTRAINT [FK_job_postings_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[media] ADD CONSTRAINT [FK_media_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[media] ADD CONSTRAINT [FK_media_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[media] ADD CONSTRAINT [FK_media_folder_id_fkey] FOREIGN KEY ([folder_id]) REFERENCES [dbo].[media_folders] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[media] ADD CONSTRAINT [FK_media_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[media_folders] ADD CONSTRAINT [FK_media_folders_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[media_folders] ADD CONSTRAINT [FK_media_folders_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[media_folders] ADD CONSTRAINT [FK_media_folders_parent_id_fkey] FOREIGN KEY ([parent_id]) REFERENCES [dbo].[media_folders] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[media_folders] ADD CONSTRAINT [FK_media_folders_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[navigation_items] ADD CONSTRAINT [FK_navigation_items_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[navigation_items] ADD CONSTRAINT [FK_navigation_items_menu_id_fkey] FOREIGN KEY ([menu_id]) REFERENCES [dbo].[navigation_menus] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[navigation_items] ADD CONSTRAINT [FK_navigation_items_page_id_fkey] FOREIGN KEY ([page_id]) REFERENCES [dbo].[pages] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[navigation_items] ADD CONSTRAINT [FK_navigation_items_parent_id_fkey] FOREIGN KEY ([parent_id]) REFERENCES [dbo].[navigation_items] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[navigation_items] ADD CONSTRAINT [FK_navigation_items_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[navigation_menus] ADD CONSTRAINT [FK_navigation_menus_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[navigation_menus] ADD CONSTRAINT [FK_navigation_menus_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[news] ADD CONSTRAINT [FK_news_author_id_fkey] FOREIGN KEY ([author_id]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[news] ADD CONSTRAINT [FK_news_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[news] ADD CONSTRAINT [FK_news_category_id_fkey] FOREIGN KEY ([category_id]) REFERENCES [dbo].[categories] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[news] ADD CONSTRAINT [FK_news_cover_media_id_fkey] FOREIGN KEY ([cover_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[news] ADD CONSTRAINT [FK_news_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[news] ADD CONSTRAINT [FK_news_seo_id_fkey] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[news] ADD CONSTRAINT [FK_news_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[page_sections] ADD CONSTRAINT [FK_page_sections_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[page_sections] ADD CONSTRAINT [FK_page_sections_page_id_fkey] FOREIGN KEY ([page_id]) REFERENCES [dbo].[pages] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[page_sections] ADD CONSTRAINT [FK_page_sections_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[pages] ADD CONSTRAINT [FK_pages_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[pages] ADD CONSTRAINT [FK_pages_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[pages] ADD CONSTRAINT [FK_pages_parent_id_fkey] FOREIGN KEY ([parent_id]) REFERENCES [dbo].[pages] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[pages] ADD CONSTRAINT [FK_pages_seo_id_fkey] FOREIGN KEY ([seo_id]) REFERENCES [dbo].[seo_metadata] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[pages] ADD CONSTRAINT [FK_pages_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[redirects] ADD CONSTRAINT [FK_redirects_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[redirects] ADD CONSTRAINT [FK_redirects_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[regions] ADD CONSTRAINT [FK_regions_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[regions] ADD CONSTRAINT [FK_regions_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[role_permissions] ADD CONSTRAINT [FK_role_permissions_permission_id_fkey] FOREIGN KEY ([permission_id]) REFERENCES [dbo].[permissions] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[role_permissions] ADD CONSTRAINT [FK_role_permissions_role_id_fkey] FOREIGN KEY ([role_id]) REFERENCES [dbo].[roles] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[seo_metadata] ADD CONSTRAINT [FK_seo_metadata_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[seo_metadata] ADD CONSTRAINT [FK_seo_metadata_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[seo_metadata] ADD CONSTRAINT [FK_seo_metadata_og_image_id_fkey] FOREIGN KEY ([og_image_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[seo_metadata] ADD CONSTRAINT [FK_seo_metadata_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[site_settings] ADD CONSTRAINT [FK_site_settings_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[site_settings] ADD CONSTRAINT [FK_site_settings_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[testimonials] ADD CONSTRAINT [FK_testimonials_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[testimonials] ADD CONSTRAINT [FK_testimonials_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[testimonials] ADD CONSTRAINT [FK_testimonials_photo_media_id_fkey] FOREIGN KEY ([photo_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[testimonials] ADD CONSTRAINT [FK_testimonials_updated_by_fkey] FOREIGN KEY ([updated_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[user_roles] ADD CONSTRAINT [FK_user_roles_branch_id_fkey] FOREIGN KEY ([branch_id]) REFERENCES [dbo].[branches] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[user_roles] ADD CONSTRAINT [FK_user_roles_created_by_fkey] FOREIGN KEY ([created_by]) REFERENCES [dbo].[users] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[user_roles] ADD CONSTRAINT [FK_user_roles_region_id_fkey] FOREIGN KEY ([region_id]) REFERENCES [dbo].[regions] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[user_roles] ADD CONSTRAINT [FK_user_roles_role_id_fkey] FOREIGN KEY ([role_id]) REFERENCES [dbo].[roles] ([id]) ON DELETE NO ACTION;
ALTER TABLE [dbo].[user_roles] ADD CONSTRAINT [FK_user_roles_user_id_fkey] FOREIGN KEY ([user_id]) REFERENCES [dbo].[users] ([id]) ON DELETE CASCADE;
ALTER TABLE [dbo].[users] ADD CONSTRAINT [FK_users_avatar_media_fk] FOREIGN KEY ([avatar_media_id]) REFERENCES [dbo].[media] ([id]) ON DELETE NO ACTION;
GO

-- Sequences for reference numbers (ARQ-2026-000001, FR-2026-00001), used by the application --
CREATE SEQUENCE [dbo].[admission_reference_seq] AS BIGINT START WITH 1 INCREMENT BY 1;
CREATE SEQUENCE [dbo].[franchise_reference_seq] AS BIGINT START WITH 1 INCREMENT BY 1;
GO


/* Notes from the conversion
   Checks left to the application:
   Indexes not converted:
     - achievements_search_idx (full-text / trigram: see 03_fulltext.sql)
     - audit_logs_created_brin: CREATE INDEX audit_logs_created_brin ON public.audit_logs USING brin (created_at)
     - branches_name_trgm_idx (full-text / trigram: see 03_fulltext.sql)
     - branches_search_idx (full-text / trigram: see 03_fulltext.sql)
     - downloads_search_idx (full-text / trigram: see 03_fulltext.sql)
     - events_search_idx (full-text / trigram: see 03_fulltext.sql)
     - job_postings_search_idx (full-text / trigram: see 03_fulltext.sql)
     - media_filename_trgm_idx (full-text / trigram: see 03_fulltext.sql)
     - news_search_idx (full-text / trigram: see 03_fulltext.sql)
   Cascades turned into NO ACTION (SQL Server allows one cascade path):
     - media_folders.media_folders_parent_id_fkey
     - navigation_items.navigation_items_parent_id_fkey
*/
