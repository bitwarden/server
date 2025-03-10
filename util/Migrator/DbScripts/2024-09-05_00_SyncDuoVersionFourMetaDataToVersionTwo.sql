-- Create temporary table to store User IDs
CREATE TABLE #TempUserIDs (
    RowNum INT IDENTITY(1,1) PRIMARY KEY,
    ID UNIQUEIDENTIFIER
);

-- Populate temporary table with User IDs
INSERT INTO #TempUserIDs (ID)
SELECT Id
FROM [dbo].[User]
WHERE TwoFactorProviders LIKE '%"2":%'
  AND ISJSON(TwoFactorProviders) = 1;

DECLARE @UserBatchSize INT = 1000;
DECLARE @TotalUserRows INT = (SELECT COUNT(*) FROM #TempUserIDs);
DECLARE @UserBatchNum INT = 0;

WHILE @UserBatchNum * @UserBatchSize < @TotalUserRows
BEGIN
    -- Update Users
    UPDATE U
    SET TwoFactorProviders = JSON_MODIFY(
        JSON_MODIFY(
            U.TwoFactorProviders,
            '$."2".MetaData.ClientSecret',
            JSON_VALUE(U.TwoFactorProviders, '$."2".MetaData.SKey')
        ),
        '$."2".MetaData.ClientId',
        JSON_VALUE(U.TwoFactorProviders, '$."2".MetaData.IKey')
    )
    FROM [dbo].[User] U
    INNER JOIN #TempUserIDs T ON U.Id = T.ID
    WHERE T.RowNum > @UserBatchNum * @UserBatchSize
      AND T.RowNum <= (@UserBatchNum + 1) * @UserBatchSize;

    SET @UserBatchNum = @UserBatchNum + 1;
END

-- Clean up
DROP TABLE #TempUserIDs;

-- Create temporary table to store Organization IDs
CREATE TABLE #TempOrganizationIDs (
    RowNum INT IDENTITY(1,1) PRIMARY KEY,
    ID UNIQUEIDENTIFIER
);

-- Populate temporary table with Organization IDs
INSERT INTO #TempOrganizationIDs (ID)
SELECT Id
FROM [dbo].[Organization]
WHERE TwoFactorProviders LIKE '%"6":%'
  AND ISJSON(TwoFactorProviders) = 1;

DECLARE @OrganizationBatchSize INT = 1000;
DECLARE @TotalOrganizationRows INT = (SELECT COUNT(*) FROM #TempOrganizationIDs);
DECLARE @OrganizationBatchNum INT = 0;

WHILE @OrganizationBatchNum * @OrganizationBatchSize < @TotalOrganizationRows
BEGIN
    -- Update Organizations
    UPDATE Org
    SET TwoFactorProviders = JSON_MODIFY(
        JSON_MODIFY(
            Org.TwoFactorProviders,
            '$."6".MetaData.ClientSecret',
            JSON_VALUE(Org.TwoFactorProviders, '$."6".MetaData.SKey')
        ),
        '$."6".MetaData.ClientId',
        JSON_VALUE(Org.TwoFactorProviders, '$."6".MetaData.IKey')
    )
    FROM [dbo].[Organization] Org
    INNER JOIN #TempOrganizationIDs T ON Org.Id = T.ID
    WHERE T.RowNum > @OrganizationBatchNum * @OrganizationBatchSize
      AND T.RowNum <= (@OrganizationBatchNum + 1) * @OrganizationBatchSize;

    SET @OrganizationBatchNum = @OrganizationBatchNum + 1;
END

-- Clean up
DROP TABLE #TempOrganizationIDs;
