CREATE PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @RevisionDate DATETIME2(7) = SYSUTCDATETIME()
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT @OrganizationId = [OrganizationId]
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id

    BEGIN TRANSACTION

    UPDATE
        [dbo].[Collection]
    SET
        [AccessRuleId] = NULL,
        [RevisionDate] = @RevisionDate
    WHERE
        [AccessRuleId] = @Id

    DELETE FROM [dbo].[AccessRule] WHERE [Id] = @Id

    COMMIT TRANSACTION

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
