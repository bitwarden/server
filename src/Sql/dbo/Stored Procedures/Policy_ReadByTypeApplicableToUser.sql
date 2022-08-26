CREATE PROCEDURE [dbo].[Policy_ReadByTypeApplicableToUser]
    @UserId UNIQUEIDENTIFIER,
    @PolicyType TINYINT,
    @MinimumStatus SMALLINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PolicyApplicableToUser](@UserId, @PolicyType, @MinimumStatus)
END
