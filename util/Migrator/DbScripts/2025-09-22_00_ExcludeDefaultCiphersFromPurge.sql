CREATE OR ALTER PROCEDURE [dbo].[Cipher_DeleteByOrganizationId]
     @OrganizationId UNIQUEIDENTIFIER
 AS
 BEGIN
     SET NOCOUNT ON;

     DECLARE @BatchSize INT = 1000;

     BEGIN TRY
         BEGIN TRANSACTION;

         ---------------------------------------------------------------------
         -- 1. Delete organization ciphers that are NOT in any default
         --    user collection (Collection.Type = 1).
         ---------------------------------------------------------------------
         WHILE 1 = 1
         BEGIN
             ;WITH Target AS
             (
                 SELECT TOP (@BatchSize) C.Id
                 FROM dbo.Cipher C
                 WHERE C.OrganizationId = @OrganizationId
                   AND NOT EXISTS (
                       SELECT 1
                       FROM dbo.CollectionCipher CC2
                       INNER JOIN dbo.Collection Col2
                         ON Col2.Id = CC2.CollectionId
                         AND Col2.Type = 1  -- Default user collection
                       WHERE CC2.CipherId = C.Id
                   )
                 ORDER BY C.Id  -- Deterministic ordering (matches clustered index)
             )
             DELETE C
             FROM dbo.Cipher C
             INNER JOIN Target T ON T.Id = C.Id;

             IF @@ROWCOUNT = 0 BREAK;
         END

         ---------------------------------------------------------------------
         -- 2. Remove remaining CollectionCipher rows that reference
         --    non-default (Type = 0 / shared) collections, for ciphers
         --    that were preserved because they belong to at least one
         --    default (Type = 1) collection.
         ---------------------------------------------------------------------
         SET @BatchSize = 1000;
         WHILE 1 = 1
         BEGIN
             ;WITH ToDelete AS
             (
                 SELECT TOP (@BatchSize)
                        CC.CipherId,
                        CC.CollectionId
                 FROM dbo.CollectionCipher CC
                 INNER JOIN dbo.Collection Col
                         ON Col.Id = CC.CollectionId
                        AND Col.Type = 0  -- Non-default collections
                 INNER JOIN dbo.Cipher C
                         ON C.Id = CC.CipherId
                 WHERE C.OrganizationId = @OrganizationId
                 ORDER BY CC.CollectionId, CC.CipherId  -- Matches clustered index
             )
             DELETE CC
             FROM dbo.CollectionCipher CC
             INNER JOIN ToDelete TD
                 ON CC.CipherId = TD.CipherId
                AND CC.CollectionId = TD.CollectionId;

             IF @@ROWCOUNT = 0 BREAK;
         END

         ---------------------------------------------------------------------
         -- 3. Bump revision date (inside transaction for consistency)
         ---------------------------------------------------------------------
         EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId;

         COMMIT TRANSACTION ;

         ---------------------------------------------------------------------
         -- 4. Update storage usage (outside the transaction to avoid
         --    holding locks during long-running calculation)
         ---------------------------------------------------------------------
         EXEC [dbo].[Organization_UpdateStorage] @OrganizationId;
     END TRY
     BEGIN CATCH
         IF @@TRANCOUNT > 0
             ROLLBACK TRANSACTION;
         THROW;
     END CATCH
 END
 GO
