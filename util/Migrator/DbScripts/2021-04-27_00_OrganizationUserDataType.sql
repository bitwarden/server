-- Create OrganizationUser Type
IF NOT EXISTS (
    SELECT
        *
    FROM
        sys.types
    WHERE 
        [Name] = 'OrganizationUserType' AND
        is_user_defined = 1
)
CREATE TYPE [dbo].[OrganizationUserType] AS TABLE(
    [Id] UNIQUEIDENTIFIER,
    [OrganizationId] UNIQUEIDENTIFIER,
    [UserId] UNIQUEIDENTIFIER,
    [Email] NVARCHAR(256),
    [Key] VARCHAR(MAX),
    [Status] TINYINT,
    [Type] TINYINT,
    [AccessAll] BIT,
    [ExternalId] NVARCHAR(300),
    [CreationDate] DATETIME2(7),
    [RevisionDate] DATETIME2(7),
    [Permissions] NVARCHAR(MAX),
    [ResetPasswordKey] VARCHAR(MAX)
)
GO
