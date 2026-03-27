CREATE PROCEDURE [dbo].[AutofillTriageReport_ReadActiveWithPagination]
    @Skip INT = 0,
    @Take INT = 25
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AutofillTriageReportView]
    WHERE
        [Archived] = 0
    ORDER BY
        [CreationDate] DESC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END
