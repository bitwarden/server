CREATE PROCEDURE [dbo].[Event_ReadPageByProjectId]
    @ProjectId UNIQUEIDENTIFIER,
    @StartDate DATETIME2(7),
    @EndDate DATETIME2(7),
    @BeforeDate DATETIME2(7),
    @PageSize INT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[EventView]
    WHERE
        [Date] >= @StartDate
        AND (@BeforeDate IS NOT NULL OR [Date] <= @EndDate)
        AND (@BeforeDate IS NULL OR [Date] < @BeforeDate)
        AND [ProjectId] = @ProjectId
    ORDER BY [Date] DESC
    OFFSET 0 ROWS
    FETCH NEXT @PageSize ROWS ONLY
END
