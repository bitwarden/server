CREATE OR REPLACE PROCEDURE cipher_updatepartial(par_id uuid, par_userid uuid, par_folderid uuid, par_favorite numeric)
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
    UPDATE cipher
    SET folders =
    CASE
        WHEN par_FolderId IS NOT NULL AND folders IS NULL THEN CONCAT('{', var_UserIdKey, ':"', par_FolderId, '"', '}')
        WHEN par_FolderId IS NOT NULL THEN JSON_MODIFY(folders, var_UserIdPath, CAST (par_FolderId AS VARCHAR(50)))
        ELSE JSON_MODIFY(folders, var_UserIdPath, NULL)
    END, favorites =
    CASE
        WHEN par_Favorite = 1 AND favorites IS NULL THEN CONCAT('{', var_UserIdKey, ':true}')
        WHEN par_Favorite = 1 THEN JSON_MODIFY(favorites, var_UserIdPath, aws_sqlserver_ext.ToMsBit(1))
        ELSE JSON_MODIFY(favorites, var_UserIdPath, NULL)
    END
        WHERE id = par_Id;

    IF par_UserId IS NOT NULL THEN
        CALL user_bumpaccountrevisiondate(par_UserId);
    END IF;
END;
$procedure$
;
