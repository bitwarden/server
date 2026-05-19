-- Drop stored procedures that are no longer called anywhere.
-- OrganizationUser_ReadByUserIdWithPolicyDetails was used by PolicyService (now deleted).
-- PolicyDetails_ReadByUserId was never called anywhere.

DROP PROCEDURE IF EXISTS [dbo].[OrganizationUser_ReadByUserIdWithPolicyDetails];
GO

DROP PROCEDURE IF EXISTS [dbo].[PolicyDetails_ReadByUserId];
GO
