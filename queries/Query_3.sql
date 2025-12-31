  -- Connect to your database and run:
  SELECT * FROM [dbo].[Migration]
  WHERE ScriptName LIKE '%2025-11-19_00_UpdateMemberAccessQuery%'

  -- Then delete it:
  DELETE FROM [dbo].[Migration]
  WHERE ScriptName = 'Bit.Migrator.DbScripts.2025-11-19_00_UpdateMemberAccessQuery.sql'

    -- Drop the views created by your migration
  DROP VIEW IF EXISTS [dbo].[CollectionCipherDetailsView]
  DROP VIEW IF EXISTS [dbo].[CollectionGroupPermissionsView]
  DROP VIEW IF EXISTS [dbo].[CollectionUserPermissionsView]

  -- Drop the stored procedure
  DROP PROCEDURE IF EXISTS [dbo].[MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId]


  SELECT OU.Id as ou_id, OU.UserId as ou_userId, UV.Id as uv_id, CUP.OrganizationUserId as cup_userId, OU.AvatarColor as ou_avatar, UV.AvatarColor as uv_avatar FROM OrganizationUserUserDetailsView OU
  JOIN UserView UV on UV.Id = OU.UserId
  JOIN CollectionUserPermissionsView CUP ON CUP.OrganizationUserId = OU.[Id];

  SELECT Id, AvatarColor FROM UserView UV WHERE UV.Id = '795F4841-3A5A-4602-A915-B38500E502FC';
  SELECT Id, AvatarColor FROM [User] WHERE id = '795F4841-3A5A-4602-A915-B38500E502FC'

EXEC [dbo].[MemberAccessReport_GetMemberAccessCipherDetailsByOrganizationId] '38a2d45f-127b-49d0-84a4-b387013ff647'



SELECT * FROM [dbo].[User] WHERE Email like '%owner%';

