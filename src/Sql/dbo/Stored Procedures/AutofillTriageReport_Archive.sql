CREATE OR ALTER PROCEDURE [dbo].[AutofillTriageReport_Archive]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AutofillTriageReport]
    SET
        [Archived] = 1
    WHERE
        [Id] = @Id
END
