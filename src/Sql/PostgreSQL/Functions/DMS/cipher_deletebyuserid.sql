CREATE OR REPLACE PROCEDURE vault_dbo.cipher_deletebyuserid(par_userid uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_BatchSize NUMERIC(10, 0) DEFAULT 100;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    /* Delete ciphers */
    WHILE var_BatchSize > 0 LOOP
        /*
        [7807 - Severity CRITICAL - PostgreSQL does not support explicit transaction management in functions. Perform a manual conversion.]
        BEGIN TRANSACTION Cipher_DeleteByUserId_Ciphers
        */
        /*
        [7798 - Severity CRITICAL - PostgreSQL doesn't support TOP option in the operator DELETE. Perform a manual conversion.]
        DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Cipher]
                WHERE
                    [UserId] = @UserId
        */
        GET DIAGNOSTICS var_BatchSize = ROW_COUNT;
        COMMIT;
    END LOOP;
    /* Delete folders */
    DELETE FROM vault_dbo.folder
        WHERE userid = par_UserId;
    /* Cleanup user */
    CALL vault_dbo.user_updatestorage(par_UserId);
    CALL vault_dbo.user_bumpaccountrevisiondate(par_UserId);
END;
$procedure$
;
