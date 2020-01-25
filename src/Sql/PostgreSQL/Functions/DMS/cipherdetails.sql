CREATE OR REPLACE FUNCTION vault_dbo.cipherdetails(par_userid uuid)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
BEGIN
    DROP TABLE IF EXISTS CipherDetails$TMPTBL;
    /*
    [9996 - Severity CRITICAL - Transformer error occurred. Please submit report to developers.]
    RETURN
    SELECT
        C.[Id],
        C.[UserId],
        C.[OrganizationId],
        C.[Type],
        C.[Data],
        C.[Attachments],
        C.[CreationDate],
        C.[RevisionDate],
        CASE
            WHEN
                @UserId IS NULL
                OR C.[Favorites] IS NULL
                OR JSON_VALUE(C.[Favorites], CONCAT('$."', @UserId, '"')) IS NULL
            THEN 0
            ELSE 1
        END [Favorite],
        CASE
            WHEN
                @UserId IS NULL
                OR C.[Folders] IS NULL
            THEN NULL
            ELSE TRY_CONVERT(UNIQUEIDENTIFIER, JSON_VALUE(C.[Folders], CONCAT('$."', @UserId, '"')))
        END [FolderId]
    FROM
        [dbo].[Cipher] C
    */
END;
$function$
;
