using System.Data;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;
using DbUp.SqlServer;

namespace Bit.Migrator;

public static class SqlTableJournalExtensions
{
    public static UpgradeEngineBuilder JournalRerunableToSqlTable(this UpgradeEngineBuilder builder, string schema, string table, bool rerunable = false)
    {
        builder.Configure(c => c.Journal = new RerunableSqlTableJournal(() => c.ConnectionManager, () => c.Log, schema, table, rerunable));
        return builder;
    }
}

public class RerunableSqlTableJournal : SqlTableJournal
{
    private bool Rerunable { get; set; }

    public RerunableSqlTableJournal(Func<IConnectionManager> connectionManager, Func<IUpgradeLog> logger, string schema, string table, bool rerunable = false)
        : base(connectionManager, logger, schema, table)
    {
        Rerunable = rerunable;
    }

    public override void StoreExecutedScript(SqlScript script, Func<IDbCommand> dbCommandFactory)
    {
        EnsureTableExistsAndIsLatestVersion(dbCommandFactory);
        using (var command = GetInsertScriptCommand(dbCommandFactory, script))
        {
            command.ExecuteNonQuery();
        }
    }

    protected new IDbCommand GetInsertScriptCommand(Func<IDbCommand> dbCommandFactory, SqlScript script)
    {
        var command = dbCommandFactory();

        var scriptNameParam = command.CreateParameter();
        scriptNameParam.ParameterName = "scriptName";
        scriptNameParam.Value = script.Name;
        command.Parameters.Add(scriptNameParam);

        var scriptFilename = script.Name.Replace("Bit.Migrator.", "");
		scriptFilename = scriptFilename.Substring(scriptFilename.IndexOf('.')+1);

        var scriptFileNameParam = command.CreateParameter();
        scriptFileNameParam.ParameterName = "scriptFileName";
        scriptFileNameParam.Value = $"%{scriptFilename}";
        command.Parameters.Add(scriptFileNameParam);

        var appliedParam = command.CreateParameter();
        appliedParam.ParameterName = "applied";
        appliedParam.Value = DateTime.Now;
        command.Parameters.Add(appliedParam);

        var rerunableParam = command.CreateParameter();
        rerunableParam.ParameterName = "rerunable";
        rerunableParam.Value = Rerunable;
        command.Parameters.Add(rerunableParam);

        command.CommandText = GetInsertJournalEntrySql("@scriptName", "@applied", "@rerunable", "@scriptFileName");
        command.CommandType = CommandType.Text;
        return command;
    }

    protected string GetInsertJournalEntrySql(string @scriptName, string @applied, string @rerunable, string @scriptFileName)
    {
        return @$"IF EXISTS (SELECT * FROM {FqSchemaTableName} WHERE Rerunable = 1 AND ScriptName like {@scriptFileName})
            BEGIN
                UPDATE {FqSchemaTableName} SET ScriptName = {@scriptName}, Applied = {@applied}, Rerunable = {@rerunable} WHERE ScriptName like {@scriptFileName}
            END
        ELSE
            BEGIN
                insert into {FqSchemaTableName} (ScriptName, Applied, Rerunable) values ({@scriptName}, {@applied}, {@rerunable})
            END ";
    }

    protected override string GetJournalEntriesSql()
    {
        return @$"DECLARE @columnVariable AS NVARCHAR(max)

            SELECT @columnVariable =
            CASE WHEN EXISTS
            (
                SELECT 1 FROM sys.columns WHERE Name = N'Rerunable' AND Object_ID = Object_ID(N'dbo.Migration')
            )
            THEN
            (
                'where [Rerunable] = 0'
            )
            ELSE
            (
                ''
            )
            END

            DECLARE @SQLString AS NVARCHAR(max) = N'select [ScriptName] from dbo.Migration ' + @columnVariable + ' order by [ScriptName]'

            EXECUTE sp_executesql @SQLString";
    }
}
