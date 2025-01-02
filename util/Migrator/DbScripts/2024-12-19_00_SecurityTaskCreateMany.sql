-- Create SecurityTaskType
IF NOT EXISTS (
    SELECT
        *
    FROM
        sys.types
    WHERE
        [Name] = 'SecurityTaskType' AND
        is_user_defined = 1
)
BEGIN
CREATE TYPE [dbo].[SecurityTaskType] AS TABLE(
    [Id] UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [CipherId] UNIQUEIDENTIFIER NOT NULL,
    [Type] TINYINT NOT NULL,
    [Status] TINYINT NOT NULL,
    [CreationDate] DATETIME2(7) NOT NULL,
    [RevisionDate] DATETIME2(7) NOT NULL
);
END
GO

-- SecurityTask_CreateMany
CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_CreateMany]
    @SecurityTasksInput AS [dbo].[SecurityTaskType] READONLY
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[SecurityTask]
    (
        [Id],
        [OrganizationId],
        [CipherId],
        [Type],
        [Status],
        [CreationDate],
        [RevisionDate]
    )
    SELECT
        ST.[Id],
        ST.[OrganizationId],
        ST.[CipherId],
        ST.[Type],
        ST.[Status],
        ST.[CreationDate],
        ST.[RevisionDate]
    FROM
        @SecurityTasksInput ST
END
GO
