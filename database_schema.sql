-- ============================================
-- UPS Services Database Schema
-- SQL Server DDL Script
-- ============================================

-- Create databases (if they don't exist)
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'reports')
BEGIN
    CREATE DATABASE [reports]
END
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'siebeldb')
BEGIN
    CREATE DATABASE [siebeldb]
END
GO

-- ============================================
-- REPORTS DATABASE TABLES
-- ============================================

USE [reports]
GO

-- TRACKING_LINKS table - Stores shortened links and their metadata
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TRACKING_LINKS' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[TRACKING_LINKS] (
        [ROW_ID] [varchar](50) NOT NULL,
        [KEY] [varchar](50) NULL,
        [LINK] [varchar](max) NOT NULL,
        [DESCRIPTION] [varchar](500) NULL,
        [TYPE_CD] [varchar](10) NULL,
        [BITLY_LINK] [varchar](500) NULL,
        [CREATED_DATE] [datetime] NULL DEFAULT (getdate()),
        [UPDATED_DATE] [datetime] NULL DEFAULT (getdate()),
        CONSTRAINT [PK_TRACKING_LINKS] PRIMARY KEY CLUSTERED ([ROW_ID] ASC)
    )
END
GO

-- TRACKING_IPS table - Stores IP address geolocation data
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TRACKING_IPS' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[TRACKING_IPS] (
        [IP_ADDRESS] [varchar](45) NOT NULL,
        [COUNTRY] [varchar](100) NULL,
        [STATE] [varchar](100) NULL,
        [CITY] [varchar](100) NULL,
        [POSTALCODE] [varchar](20) NULL,
        [LAT] [decimal](10, 8) NULL,
        [LONG] [decimal](11, 8) NULL,
        [CREATED_DATE] [datetime] NULL DEFAULT (getdate()),
        [UPDATED_DATE] [datetime] NULL DEFAULT (getdate()),
        CONSTRAINT [PK_TRACKING_IPS] PRIMARY KEY CLUSTERED ([IP_ADDRESS] ASC)
    )
END
GO

-- TRACKING_CLICKS table - Stores click tracking data for links
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='TRACKING_CLICKS' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[TRACKING_CLICKS] (
        [CLICK_ID] [varchar](50) NOT NULL,
        [LINK_ROW_ID] [varchar](50) NOT NULL,
        [IP_ADDRESS] [varchar](45) NULL,
        [USER_AGENT] [varchar](500) NULL,
        [REFERER] [varchar](500) NULL,
        [CLICK_DATE] [datetime] NULL DEFAULT (getdate()),
        [COUNTRY] [varchar](100) NULL,
        [STATE] [varchar](100) NULL,
        [CITY] [varchar](100) NULL,
        CONSTRAINT [PK_TRACKING_CLICKS] PRIMARY KEY CLUSTERED ([CLICK_ID] ASC),
        CONSTRAINT [FK_TRACKING_CLICKS_LINKS] FOREIGN KEY ([LINK_ROW_ID]) 
            REFERENCES [dbo].[TRACKING_LINKS] ([ROW_ID]) ON DELETE CASCADE
    )
END
GO

-- ============================================
-- SIEBEL DATABASE TABLES
-- ============================================

USE [siebeldb]
GO

-- S_LANG table - Language codes and translations
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='S_LANG' AND xtype='U')
BEGIN
    CREATE TABLE [dbo].[S_LANG] (
        [LANG_CD] [varchar](10) NOT NULL,
        [X_CODE] [varchar](10) NULL,
        [DESCRIPTION] [varchar](100) NULL,
        [ACTIVE_FLG] [char](1) NULL DEFAULT ('Y'),
        CONSTRAINT [PK_S_LANG] PRIMARY KEY CLUSTERED ([LANG_CD] ASC)
    )
END
GO

-- ============================================
-- INDEXES FOR PERFORMANCE
-- ============================================

USE [reports]
GO

-- Indexes for TRACKING_LINKS
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_LINKS_LINK')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_LINKS_LINK] ON [dbo].[TRACKING_LINKS] ([LINK])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_LINKS_KEY')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_LINKS_KEY] ON [dbo].[TRACKING_LINKS] ([KEY])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_LINKS_BITLY')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_LINKS_BITLY] ON [dbo].[TRACKING_LINKS] ([BITLY_LINK])
END
GO

-- Indexes for TRACKING_IPS
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_IPS_COUNTRY')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_IPS_COUNTRY] ON [dbo].[TRACKING_IPS] ([COUNTRY])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_IPS_CITY')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_IPS_CITY] ON [dbo].[TRACKING_IPS] ([CITY])
END
GO

-- Indexes for TRACKING_CLICKS
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_CLICKS_LINK_ROW_ID')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_CLICKS_LINK_ROW_ID] ON [dbo].[TRACKING_CLICKS] ([LINK_ROW_ID])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_CLICKS_IP')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_CLICKS_IP] ON [dbo].[TRACKING_CLICKS] ([IP_ADDRESS])
END
GO

IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_TRACKING_CLICKS_DATE')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_TRACKING_CLICKS_DATE] ON [dbo].[TRACKING_CLICKS] ([CLICK_DATE])
END
GO

-- ============================================
-- SAMPLE DATA
-- ============================================

USE [siebeldb]
GO

-- Insert sample language codes
IF NOT EXISTS (SELECT * FROM [dbo].[S_LANG] WHERE [LANG_CD] = 'EN')
BEGIN
    INSERT INTO [dbo].[S_LANG] ([LANG_CD], [X_CODE], [DESCRIPTION]) VALUES 
    ('EN', 'en', 'English'),
    ('ES', 'es', 'Spanish'),
    ('FR', 'fr', 'French'),
    ('DE', 'de', 'German'),
    ('IT', 'it', 'Italian'),
    ('PT', 'pt', 'Portuguese'),
    ('RU', 'ru', 'Russian'),
    ('JA', 'ja', 'Japanese'),
    ('KO', 'ko', 'Korean'),
    ('ZH', 'zh', 'Chinese')
END
GO

-- ============================================
-- STORED PROCEDURES (Optional)
-- ============================================

USE [reports]
GO

-- Procedure to get link statistics
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_GetLinkStats')
    DROP PROCEDURE [dbo].[sp_GetLinkStats]
GO

CREATE PROCEDURE [dbo].[sp_GetLinkStats]
    @LinkRowId VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        tl.ROW_ID,
        tl.LINK,
        tl.DESCRIPTION,
        tl.BITLY_LINK,
        COUNT(tc.CLICK_ID) as TotalClicks,
        COUNT(DISTINCT tc.IP_ADDRESS) as UniqueIPs,
        COUNT(DISTINCT tc.COUNTRY) as UniqueCountries
    FROM [dbo].[TRACKING_LINKS] tl
    LEFT JOIN [dbo].[TRACKING_CLICKS] tc ON tl.ROW_ID = tc.LINK_ROW_ID
    WHERE tl.ROW_ID = @LinkRowId
    GROUP BY tl.ROW_ID, tl.LINK, tl.DESCRIPTION, tl.BITLY_LINK
END
GO

-- Procedure to clean up old tracking data
IF EXISTS (SELECT * FROM sys.objects WHERE type = 'P' AND name = 'sp_CleanupOldTrackingData')
    DROP PROCEDURE [dbo].[sp_CleanupOldTrackingData]
GO

CREATE PROCEDURE [dbo].[sp_CleanupOldTrackingData]
    @DaysOld INT = 365
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @CutoffDate DATETIME = DATEADD(day, -@DaysOld, GETDATE())
    
    -- Delete old clicks
    DELETE FROM [dbo].[TRACKING_CLICKS] 
    WHERE [CLICK_DATE] < @CutoffDate
    
    -- Delete old links that have no clicks
    DELETE FROM [dbo].[TRACKING_LINKS] 
    WHERE [CREATED_DATE] < @CutoffDate
    AND [ROW_ID] NOT IN (SELECT DISTINCT [LINK_ROW_ID] FROM [dbo].[TRACKING_CLICKS])
    
    -- Delete old IP records that are not referenced
    DELETE FROM [dbo].[TRACKING_IPS] 
    WHERE [CREATED_DATE] < @CutoffDate
    AND [IP_ADDRESS] NOT IN (SELECT DISTINCT [IP_ADDRESS] FROM [dbo].[TRACKING_CLICKS] WHERE [IP_ADDRESS] IS NOT NULL)
END
GO

-- ============================================
-- VIEWS FOR REPORTING
-- ============================================

USE [reports]
GO

-- View for link performance summary
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_LinkPerformance')
    DROP VIEW [dbo].[vw_LinkPerformance]
GO

CREATE VIEW [dbo].[vw_LinkPerformance]
AS
SELECT 
    tl.ROW_ID,
    tl.LINK,
    tl.DESCRIPTION,
    tl.TYPE_CD,
    tl.BITLY_LINK,
    tl.CREATED_DATE,
    COUNT(tc.CLICK_ID) as TotalClicks,
    COUNT(DISTINCT tc.IP_ADDRESS) as UniqueVisitors,
    COUNT(DISTINCT tc.COUNTRY) as CountriesReached,
    MAX(tc.CLICK_DATE) as LastClickDate
FROM [dbo].[TRACKING_LINKS] tl
LEFT JOIN [dbo].[TRACKING_CLICKS] tc ON tl.ROW_ID = tc.LINK_ROW_ID
GROUP BY tl.ROW_ID, tl.LINK, tl.DESCRIPTION, tl.TYPE_CD, tl.BITLY_LINK, tl.CREATED_DATE
GO

-- View for geographic distribution
IF EXISTS (SELECT * FROM sys.views WHERE name = 'vw_GeographicDistribution')
    DROP VIEW [dbo].[vw_GeographicDistribution]
GO

CREATE VIEW [dbo].[vw_GeographicDistribution]
AS
SELECT 
    tc.COUNTRY,
    tc.STATE,
    tc.CITY,
    COUNT(*) as ClickCount,
    COUNT(DISTINCT tc.IP_ADDRESS) as UniqueIPs,
    COUNT(DISTINCT tc.LINK_ROW_ID) as LinksClicked
FROM [dbo].[TRACKING_CLICKS] tc
WHERE tc.COUNTRY IS NOT NULL
GROUP BY tc.COUNTRY, tc.STATE, tc.CITY
GO

PRINT 'Database schema creation completed successfully!'
PRINT 'Created databases: reports, siebeldb'
PRINT 'Created tables: TRACKING_LINKS, TRACKING_IPS, TRACKING_CLICKS, S_LANG'
PRINT 'Created indexes, stored procedures, and views for optimal performance'
