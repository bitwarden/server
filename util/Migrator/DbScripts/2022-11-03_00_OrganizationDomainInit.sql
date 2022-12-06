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
    [JobRunCount]      TINYINT          NOT NULL
    CONSTRAINT [PK_OrganizationDomain] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganzationDomain_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
)
END
GO

-- Create View
CREATE OR ALTER VIEW [dbo].[OrganizationDomainView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationDomain]
GO

-- Organization Domain CRUD SPs
-- Create
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Txt    VARCHAR(MAX),
    @DomainName NVARCHAR(255),
    @CreationDate   DATETIME2(7),
    @VerifiedDate   DATETIME2(7),
    @NextRunDate    DATETIME2(7),
    @JobRunCount   TINYINT
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
        [JobRunCount]
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
        @JobRunCount
    )
END
GO

--Update
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Txt VARCHAR(MAX),
    @DomainName NVARCHAR(255),
    @CreationDate   DATETIME2(7),
    @VerifiedDate   DATETIME2(7),
    @NextRunDate    DATETIME2(7),
    @JobRunCount   TINYINT
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    [dbo].[OrganizationDomain]
SET
    [OrganizationId] = @OrganizationId,
    [Txt] = @Txt,
    [DomainName] = @DomainName,
    [CreationDate] = @CreationDate,
    [VerifiedDate] = @VerifiedDate,
    [NextRunDate] = @NextRunDate,
    [JobRunCount] = @JobRunCount
WHERE
    [Id] = @Id
END
GO
    
--Read
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_ReadById]
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

--Delete
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

DELETE
FROM
    [dbo].[OrganizationDomain]
WHERE
    [Id] = @Id
END
GO

-- SP to get claimed domain by domain name
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_ReadByClaimedDomain]
    @DomainName NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationDomain]
WHERE
    [DomainName] = @DomainName
  AND
    [VerifiedDate] IS NOT NULL
END
GO

-- SP to get domains by OrganizationId
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationDomain]
WHERE
    [OrganizationId] = @OrganizationId
END
GO

-- SP to get Organization SSO Provider details by Email
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomainSsoDetails_ReadByEmail]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON
        
    DECLARE @Domain NVARCHAR(256)

SELECT @Domain = SUBSTRING(@Email, CHARINDEX( '@', @Email) + 1, LEN(@Email))

SELECT
    O.Id AS OrganizationId,
    O.[Name] AS OrganizationName,
    O.UseSso AS SsoAvailable,
    P.Enabled AS SsoRequired,
    O.Identifier AS OrganizationIdentifier,
    OD.VerifiedDate,
    P.[Type] AS PolicyType,
    OD.DomainName
FROM
    [dbo].[OrganizationView] O
    INNER JOIN [dbo].[OrganizationDomainView] OD
ON O.Id = OD.OrganizationId
    INNER JOIN [dbo].[PolicyView] P
    ON O.Id = P.OrganizationId
WHERE OD.DomainName = @Domain
  AND O.Enabled = 1
  AND P.[Type] = 4 -- SSO Type
END
GO