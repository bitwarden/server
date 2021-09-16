IF OBJECT_ID('[dbo].[Policy_CountByTypeApplicableToUser]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Policy_CountByTypeApplicableToUser]
END
GO

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
