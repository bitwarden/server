-- Update PhishingDomain table to use Checksum instead of dates
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'PhishingDomain' AND COLUMN_NAME = 'CreationDate')
BEGIN
    -- Add Checksum column
    ALTER TABLE [dbo].[PhishingDomain]
    ADD [Checksum] NVARCHAR(64) NULL;

    -- Drop old columns
    ALTER TABLE [dbo].[PhishingDomain]
    DROP COLUMN [CreationDate], [RevisionDate];
END
GO

-- Update PhishingDomain_Create stored procedure
IF OBJECT_ID('[dbo].[PhishingDomain_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PhishingDomain_Create]
END
GO

CREATE PROCEDURE [dbo].[PhishingDomain_Create]
    @Id UNIQUEIDENTIFIER,
    @Domain NVARCHAR(255),
    @Checksum NVARCHAR(64)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PhishingDomain]
    (
        [Id],
        [Domain],
        [Checksum]
    )
    VALUES
    (
        @Id,
        @Domain,
        @Checksum
    )
END
GO

-- Create PhishingDomain_ReadChecksum stored procedure
IF OBJECT_ID('[dbo].[PhishingDomain_ReadChecksum]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PhishingDomain_ReadChecksum]
END
GO

CREATE PROCEDURE [dbo].[PhishingDomain_ReadChecksum]
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        [Checksum]
    FROM
        [dbo].[PhishingDomain]
END
GO 