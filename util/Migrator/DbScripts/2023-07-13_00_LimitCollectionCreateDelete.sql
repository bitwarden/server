--Add column 'LimitCollectionCdOwnerAdmin' to 'Organization' table
IF COL_LENGTH('[dbo].[Organization]', 'LimitCollectionCdOwnerAdmin') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[Organization]
    ADD
        [LimitCollectionCdOwnerAdmin] BIT NOT NULL CONSTRAINT [DF_Organization_LimitCollectionCdOwnerAdmin] DEFAULT (1)
END
GO