CREATE PROCEDURE [dbo].[AccessRule_ReadDetailsById]
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
