CREATE TYPE [dbo].[OrganizationUserType] AS TABLE(
    [Id] UNIQUEIDENTIFIER,
    [OrganizationId] UNIQUEIDENTIFIER,
    [UserId] UNIQUEIDENTIFIER,
    [Email] NVARCHAR(256),
    [Key] VARCHAR(MAX),
    [Status] SMALLINT,
    [Type] TINYINT,
    [AccessAll] BIT,
    [ExternalId] NVARCHAR(300),
    [CreationDate] DATETIME2(7),
    [RevisionDate] DATETIME2(7),
    [Permissions] NVARCHAR(MAX),
    [ResetPasswordKey] VARCHAR(MAX)
)
