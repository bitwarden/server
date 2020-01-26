CREATE OR REPLACE PROCEDURE vault_dbo.user_updaterenewalreminderdate(par_id uuid, par_renewalreminderdate timestamp without time zone)
 LANGUAGE plpgsql
AS $procedure$
BEGIN
    /*
    [7810 - Severity CRITICAL - PostgreSQL doesn't support the SET NOCOUNT. If need try another way to send message back to the client application.]
    SET NOCOUNT ON
    */
    UPDATE vault_dbo."User"
    SET renewalreminderdate = par_RenewalReminderDate
        WHERE id = par_Id;
END;
$procedure$
;
