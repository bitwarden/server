CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id
        AND [DeletedDate] IS NULL
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [OrganizationId] = @OrganizationId
        AND [DeletedDate] IS NULL
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id
        AND [DeletedDate] IS NULL

    SELECT [Id]
    FROM [dbo].[Collection]
    WHERE [AccessRuleId] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_ReadDetailsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[AccessRule]
    WHERE [OrganizationId] = @OrganizationId
        AND [DeletedDate] IS NULL

    SELECT
        [AccessRuleId],
        [Id] AS [CollectionId]
    FROM [dbo].[Collection]
    WHERE [OrganizationId] = @OrganizationId
        AND [AccessRuleId] IS NOT NULL
END
GO
