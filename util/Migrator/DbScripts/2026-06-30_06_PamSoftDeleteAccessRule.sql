CREATE OR ALTER PROCEDURE [dbo].[AccessRule_DeleteById]
    @Id UNIQUEIDENTIFIER,
    @DeletedBy UNIQUEIDENTIFIER = NULL,
    @DeletedDate DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Now DATETIME2(7) = COALESCE(@DeletedDate, SYSUTCDATETIME())
    DECLARE @OrganizationId UNIQUEIDENTIFIER

    SELECT @OrganizationId = [OrganizationId]
    FROM [dbo].[AccessRule]
    WHERE [Id] = @Id
        AND [DeletedDate] IS NULL

    -- Soft-delete: stamp DeletedDate/DeletedBy and PRESERVE the Collection.AccessRuleId links so the synthesized
    -- rule_deleted audit event can still scope through them. RevisionDate is deliberately left untouched so the delete
    -- is not mistaken for an edit by the rule_updated projection (which fires on RevisionDate > CreationDate). Every
    -- gating / governing read excludes DeletedDate IS NOT NULL, so the rule stops governing access. Idempotent:
    -- deleting an already-deleted rule is a no-op (the WHERE filters it out, leaving @OrganizationId null).
    UPDATE [dbo].[AccessRule]
    SET [DeletedDate] = @Now,
        [DeletedBy] = @DeletedBy
    WHERE [Id] = @Id
        AND [DeletedDate] IS NULL

    IF @OrganizationId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
    END
END
GO
