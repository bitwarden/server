CREATE PROCEDURE [dbo].[AuthRequest_ReadAdminApprovalsByIds]
	@OrganizationId UNIQUEIDENTIFIER,
	@Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

SELECT
    ar.*, ou.[Email], ou.[OrganizationId], ou.[Id] AS [OrganizationUserId]
FROM
    [dbo].[AuthRequestView] ar
    INNER JOIN
    [dbo].[OrganizationUser] ou ON ou.[UserId] = ar.[UserId] AND ou.[OrganizationId] = @OrganizationId
WHERE
    ar.[Type] = 2 -- AdminApproval
  AND
    ar.[Id] IN (SELECT [Id] FROM @Ids)
END;