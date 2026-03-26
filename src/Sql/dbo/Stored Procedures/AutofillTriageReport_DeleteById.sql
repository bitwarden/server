CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM
        [dbo].[AutofillTriageReport]
    WHERE
        [Id] = @Id
END
