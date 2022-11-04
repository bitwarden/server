-- Create Organization Domain table
IF OBJECT_ID('[dbo].[OrganizationDomain]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[OrganizationDomain]
END
GO

IF OBJECT_ID('[dbo].[OrganizationDomain]') IS NULL
BEGIN
CREATE TABLE [dbo].[OrganizationDomain] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER NOT NULL,
    [Txt]               VARCHAR(MAX)     NOT NULL,
    [DomainName]        NVARCHAR(255)    NOT NULL,
    [CreationDate]      DATETIME2(7)     NOT NULL,
    [VerifiedDate]      DATETIME2(7)     NULL,
    [NextRunDate]       DATETIME2(7)     NOT NULL,
    [NextRunCount]      TINYINT          NOT NULL,
    [Active]            BIT              NOT NULL,
    CONSTRAINT [PK_OrganizationDomain] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganzationDomain_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
)
END
GO

-- Create View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationDomainView')
BEGIN
    DROP VIEW [dbo].[OrganizationDomainView]
END
GO

CREATE VIEW [dbo].[OrganizationDomainView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationDomain]
GO

-- Organization Domain CRUD SPs
-- Create
IF OBJECT_ID('[dbo].[OrganizationDomain_Create]') IS NOT NULL
BEGIN 
    DROP PROCEDURE [dbo].[OrganizationDomain_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationDomain_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Txt    VARCHAR(MAX),
    @DomainName NVARCHAR(255),
    @CreationDate   DATETIME2(7),
    @VerifiedDate   DATETIME2(7),
    @NextRunDate    DATETIME2(7),
    @NextRunCount   TINYINT,
    @Active BIT
AS
BEGIN
    SET NOCOUNT ON
        
    INSERT INTO [dbo].[OrganizationDomain]
    (
        [Id],
        [OrganizationId],
        [Txt],
        [DomainName],
        [CreationDate],
        [VerifiedDate],
        [NextRunDate],
        [NextRunCount],
        [Active]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Txt,
        @DomainName,
        @CreationDate,
        @VerifiedDate,
        @NextRunDate,
        @NextRunCount,
        @Active
    )
END
GO

--Update
IF OBJECT_ID('[dbo].[OrganizationDomain_Update]') IS NOT NULL
BEGIN 
    DROP PROCEDURE [dbo].[OrganizationDomain_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationDomain_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @VerifiedDate   DATETIME2(7),
    @NextRunDate    DATETIME2(7),
    @NextRunCount   TINYINT
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[OrganizationDomain]
SET
    [VerifiedDate] = @VerifiedDate,
    [NextRunDate] = @NextRunDate,
    [NextRunCount] = @NextRunCount
WHERE
    [Id] = @Id
END
GO
    
--Read
IF OBJECT_ID('[dbo].[OrganizationDomain_ReadById]') IS NOT NULL
BEGIN 
    DROP PROCEDURE [dbo].[OrganizationDomain_ReadById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationDomain_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationDomain]
WHERE
    [Id] = @Id
END
GO

--Deactivate
IF OBJECT_ID('[dbo].[OrganizationDomain_Deactivate]') IS NOT NULL
BEGIN 
    DROP PROCEDURE [dbo].[OrganizationDomain_Deactivate]
END
GO

CREATE PROCEDURE [dbo].[OrganizationDomain_Deactivate]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[OrganizationDomain]
SET
    [Active] = 0 -- False
WHERE
    [Id] = @Id
END
GO