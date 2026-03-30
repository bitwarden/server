CREATE PROCEDURE [dbo].[AutofillTriageReport_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AutofillTriageReportView]
    WHERE
        [Id] = @Id
END
