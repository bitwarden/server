-- Migrate existing DisableSend (6) and SendOptions (7) policies into new SendControls (20)
-- EDD-compatible: only inserts new rows, never modifies existing data

DECLARE @SendControlsType TINYINT = 20;
DECLARE @DisableSendType  TINYINT = 6;
DECLARE @SendOptionsType  TINYINT = 7;
DECLARE @BatchSize        INT     = 2000;
DECLARE @RowsAffected     INT     = 1;

WHILE @RowsAffected > 0
BEGIN
    INSERT INTO [dbo].[Policy] (
        [Id], [OrganizationId], [Type], [Enabled], [Data], [CreationDate], [RevisionDate]
    )
    SELECT TOP (@BatchSize)
        NEWID(),
        combined.OrganizationId,
        @SendControlsType,
        -- Policy is enabled if either old policy was enabled
        CASE WHEN ISNULL(combined.DisableSendEnabled, 0) = 1
              OR ISNULL(combined.SendOptionsEnabled, 0) = 1
             THEN 1 ELSE 0 END,
        -- Build JSON: use ISJSON guard for SendOptions.Data
        N'{"disableSend":' +
            CASE WHEN ISNULL(combined.DisableSendEnabled, 0) = 1
                 THEN N'true' ELSE N'false' END +
        N',"disableHideEmail":' +
            CASE WHEN combined.SendOptionsData IS NOT NULL
                      AND ISJSON(combined.SendOptionsData) = 1
                      AND JSON_VALUE(combined.SendOptionsData, '$.disableHideEmail') = 'true'
                 THEN N'true' ELSE N'false' END +
        N'}',
        GETUTCDATE(),
        GETUTCDATE()
    FROM (
        SELECT DISTINCT
             COALESCE(ds.OrganizationId, so.OrganizationId) AS OrganizationId,
             ds.Enabled AS DisableSendEnabled,
             so.Enabled AS SendOptionsEnabled,
             so.Data AS SendOptionsData
        FROM 
            [dbo].[Policy] ds
        LEFT JOIN
            [dbo].[Policy] so 
        ON ds.OrganizationId = so.OrganizationId 
            AND so.Type = @SendOptionsType
        WHERE
            ds.Type = @DisableSendType
        UNION
        SELECT
            so.OrganizationId, 
            NULL, 
            so.Enabled, 
            so.Data
        FROM 
            [dbo].[Policy] so
        WHERE 
            so.Type = @SendOptionsType
            AND NOT EXISTS (
                SELECT
                    1 
                FROM
                    [dbo].[Policy] ds 
                WHERE
                    ds.OrganizationId = so.OrganizationId 
                    AND ds.Type = @DisableSendType
        )
    ) combined
    -- Skip orgs that already have a SendControls row
    WHERE NOT EXISTS (
        SELECT 1 FROM [dbo].[Policy] sc
        WHERE sc.OrganizationId = combined.OrganizationId
          AND sc.Type = @SendControlsType
    );

    SET @RowsAffected = @@ROWCOUNT;
END
