-- Create PhishingDomain table
IF OBJECT_ID('[dbo].[PhishingDomain]') IS NULL
BEGIN
    CREATE TABLE [dbo].[PhishingDomain] (
        [Id]            UNIQUEIDENTIFIER    NOT NULL,
        [Domain]        NVARCHAR(255)       NOT NULL,
        [CreationDate]  DATETIME2(7)        NOT NULL,
        [RevisionDate]  DATETIME2(7)        NOT NULL,
        CONSTRAINT [PK_PhishingDomain] PRIMARY KEY CLUSTERED ([Id] ASC)
    );

    CREATE NONCLUSTERED INDEX [IX_PhishingDomain_Domain]
        ON [dbo].[PhishingDomain]([Domain] ASC);
END
GO

-- Create PhishingDomain_ReadAll stored procedure
IF OBJECT_ID('[dbo].[PhishingDomain_ReadAll]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PhishingDomain_ReadAll]
END
GO

CREATE PROCEDURE [dbo].[PhishingDomain_ReadAll]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Domain]
    FROM
        [dbo].[PhishingDomain]
    ORDER BY
        [Domain] ASC
END
GO

-- Create PhishingDomain_DeleteAll stored procedure
IF OBJECT_ID('[dbo].[PhishingDomain_DeleteAll]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PhishingDomain_DeleteAll]
END
GO

CREATE PROCEDURE [dbo].[PhishingDomain_DeleteAll]
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[PhishingDomain]
END
GO

-- Create PhishingDomain_Create stored procedure
IF OBJECT_ID('[dbo].[PhishingDomain_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[PhishingDomain_Create]
END
GO

CREATE PROCEDURE [dbo].[PhishingDomain_Create]
    @Id UNIQUEIDENTIFIER,
    @Domain NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[PhishingDomain]
    (
        [Id],
        [Domain],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Domain,
        @CreationDate,
        @RevisionDate
    )
END
GO 