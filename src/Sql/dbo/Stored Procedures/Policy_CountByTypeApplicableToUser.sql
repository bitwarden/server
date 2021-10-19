CREATE PROCEDURE [dbo].[Policy_CountByTypeApplicableToUser]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT COUNT(1)
    FROM [dbo].[PolicyApplicableToUser](@UserId, @PolicyType, @MinimumStatus)
END
