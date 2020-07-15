CREATE OR REPLACE PROCEDURE cipher_deletebyorganizationid(par_organizationid uuid)
 LANGUAGE plpgsql
AS $procedure$
DECLARE
    var_BatchSize NUMERIC(10, 0) DEFAULT 100;
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    /* Delete collection ciphers */
    WHILE var_BatchSize > 0 LOOP
        /*
        [7807 - Severity CRITICAL - PostgreSQL does not support explicit transaction management in functions. Perform a manual conversion.]
        BEGIN TRANSACTION Cipher_DeleteByOrganizationId_CC
        */
        /*
        [7798 - Severity CRITICAL - PostgreSQL doesn't support TOP option in the operator DELETE. Perform a manual conversion.]
        DELETE TOP(@BatchSize) CC
                FROM
                    [dbo].[CollectionCipher] CC
                INNER JOIN
                    [dbo].[Collection] C ON C.[Id] = CC.[CollectionId]
                WHERE
                    C.[OrganizationId] = @OrganizationId
        */
        GET DIAGNOSTICS var_BatchSize = ROW_COUNT;
        COMMIT;
    END LOOP;
    /* Reset batch size */
    var_BatchSize := 100;
    /* Delete ciphers */

    WHILE var_BatchSize > 0 LOOP
        /*
        [7807 - Severity CRITICAL - PostgreSQL does not support explicit transaction management in functions. Perform a manual conversion.]
        BEGIN TRANSACTION Cipher_DeleteByOrganizationId
        */
        /*
        [7798 - Severity CRITICAL - PostgreSQL doesn't support TOP option in the operator DELETE. Perform a manual conversion.]
        DELETE TOP(@BatchSize)
                FROM
                    [dbo].[Cipher]
                WHERE
                    [OrganizationId] = @OrganizationId
        */
        GET DIAGNOSTICS var_BatchSize = ROW_COUNT;
        COMMIT;
    END LOOP;
    /* Cleanup organization */
    CALL organization_updatestorage(par_OrganizationId);
    CALL user_bumpaccountrevisiondatebyorganizationid(par_OrganizationId);
END;
$procedure$
;
