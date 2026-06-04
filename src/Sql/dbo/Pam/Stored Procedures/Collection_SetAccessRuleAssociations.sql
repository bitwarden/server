CREATE PROCEDURE [dbo].[Collection_SetAccessRuleAssociations]
    @AccessRuleId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @ToAssign AS [dbo].[GuidIdArray] READONLY,
    @ToClear AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @RevisionDate DATETIME2(7) = SYSUTCDATETIME()

    BEGIN TRANSACTION

    UPDATE
        C
    SET
        C.[AccessRuleId] = NULL,
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToClear T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId
        AND C.[AccessRuleId] = @AccessRuleId

    UPDATE
        C
    SET
        C.[AccessRuleId] = @AccessRuleId,
        C.[RevisionDate] = @RevisionDate
    FROM
        [dbo].[Collection] C
    INNER JOIN
        @ToAssign T ON T.[Id] = C.[Id]
    WHERE
        C.[OrganizationId] = @OrganizationId

    COMMIT TRANSACTION

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END
