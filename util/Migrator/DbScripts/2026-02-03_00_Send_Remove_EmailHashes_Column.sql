-- Update Send table to remove EmailHashes Column
IF COL_LENGTH('[dbo].[Send]', 'EmailHashes') IS NOT NULL
BEGIN
ALTER TABLE [dbo].[Send]
    DROP COLUMN [EmailHashes];
END
GO
