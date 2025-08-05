CREATE PROCEDURE [dbo].[OrganizationUser_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @Ids

    -- Migrate DefaultUserCollection to SharedCollection
    EXEC [dbo].[Collection_UpdateTypeForDeletedOrganizationUsers] @Ids

    DECLARE @UserAndOrganizationIds [dbo].[TwoGuidIdArray]

    INSERT INTO @UserAndOrganizationIds
        (Id1, Id2)
   SELECT
