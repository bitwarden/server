-- Sync existing data
UPDATE [dbo].[Organization]
SET
  [LimitCollectionCreation] = [LimitCollectionCreationDeletion],
  [LimitCollectionDeletion] = [LimitCollectionCreationDeletion]
GO
