-- Sync existing data
UPDATE [dbo].[Organization]
SET
  [LimitCollectionCreation] = 1,
  [LimitCollectionDeletion] = 1
WHERE [LimitCollectionCreationDeletion] = 1
GO

