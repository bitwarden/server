CREATE PROCEDURE [dbo].[Event_ReadPageByCipherId]
    @OrganizationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
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
        AND (
            (@OrganizationId IS NULL AND [OrganizationId] IS NULL)
            OR (@OrganizationId IS NOT NULL AND [OrganizationId] = @OrganizationId)
        )
        AND (
            (@UserId IS NULL AND [UserId] IS NULL)
            OR (@UserId IS NOT NULL AND [UserId] = @UserId)
        )
        AND [CipherId]  = @CipherId
    ORDER BY [Date] DESC
    OFFSET 0 ROWS
    FETCH NEXT @PageSize ROWS ONLY
END