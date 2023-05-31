CREATE PROCEDURE [dbo].[AuthRequest_ReadPendingByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
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
        ar.[ResponseDate] IS NULL 
    AND 
        ar.[Type] = 2 -- AdminApproval
END;