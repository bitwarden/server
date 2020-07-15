CREATE OR REPLACE PROCEDURE cipher_move(par_ids guididarray, par_folderid uuid, par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_UserIdKey VARCHAR(50) DEFAULT CONCAT('"', par_UserId, '"');
    var_UserIdPath VARCHAR(50) DEFAULT CONCAT('$.', var_UserIdKey);
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    PERFORM usercipherdetails(par_UserId);
    WITH idstomovecte
    AS (SELECT
        id
        FROM usercipherdetails$tmptbl
        WHERE Edit = 1 AND id IN (SELECT
            *
            FROM "par_Ids$aws$tmp"))
    UPDATE cipher
    SET folders =
    CASE
        WHEN par_FolderId IS NOT NULL AND folders IS NULL THEN CONCAT('{', var_UserIdKey, ':"', par_FolderId, '"', '}')
        WHEN par_FolderId IS NOT NULL THEN JSON_MODIFY(folders, var_UserIdPath, CAST (par_FolderId AS VARCHAR(50)))
        ELSE JSON_MODIFY(folders, var_UserIdPath, NULL)
    END
        WHERE id IN (SELECT
            *
            FROM idstomovecte);
    CALL user_bumpaccountrevisiondate(par_UserId);
END;
$procedure$
;
