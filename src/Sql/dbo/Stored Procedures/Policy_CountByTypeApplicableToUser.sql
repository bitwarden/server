CREATE PROCEDURE [dbo].[Policy_CountByTypeApplicableToUser]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT COUNT(1)
    FROM [dbo].[PolicyApplicableToUser](@UserId, @PolicyType, @MinimumStatus)
END
