using System.Data;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;
using DbUp.SqlServer;

namespace Bit.Migrator;

public static class SqlTableJournalExtensions
{
    public static UpgradeEngineBuilder JournalRepeatableToSqlTable(this UpgradeEngineBuilder builder, string schema, string table, bool repeatable = false)
    {
        builder.Configure(c => c.Journal = new RepeatableSqlTableJournal(() => c.ConnectionManager, () => c.Log, schema, table, repeatable));
        return builder;
    }
}

public class RepeatableSqlTableJournal : SqlTableJournal
{
    private bool Repeatable { get; set; }

    public RepeatableSqlTableJournal(Func<IConnectionManager> connectionManager, Func<IUpgradeLog> logger, string schema, string table, bool repeatable = false)
        : base(connectionManager, logger, schema, table)
    {
        Repeatable = repeatable;
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
        scriptFilename = scriptFilename.Substring(scriptFilename.IndexOf('.') + 1);

        var scriptFileNameParam = command.CreateParameter();
        scriptFileNameParam.ParameterName = "scriptFileName";
        scriptFileNameParam.Value = $"%{scriptFilename}";
        command.Parameters.Add(scriptFileNameParam);

        var appliedParam = command.CreateParameter();
        appliedParam.ParameterName = "applied";
        appliedParam.Value = DateTime.Now;
        command.Parameters.Add(appliedParam);

        var repeatableParam = command.CreateParameter();
        repeatableParam.ParameterName = "repeatable";
        repeatableParam.Value = Repeatable;
        command.Parameters.Add(repeatableParam);

        command.CommandText = GetInsertJournalEntrySql("@scriptName", "@applied", "@repeatable", "@scriptFileName");
        command.CommandType = CommandType.Text;
        return command;
    }

    protected string GetInsertJournalEntrySql(string @scriptName, string @applied, string @repeatable, string @scriptFileName)
    {
        return @$"IF EXISTS (SELECT * FROM {FqSchemaTableName} WHERE Repeatable = 1 AND ScriptName like {@scriptFileName})
            BEGIN
                UPDATE {FqSchemaTableName} SET ScriptName = {@scriptName}, Applied = {@applied}, Repeatable = {@repeatable} WHERE ScriptName like {@scriptFileName}
            END
        ELSE
            BEGIN
                insert into {FqSchemaTableName} (ScriptName, Applied, Repeatable) values ({@scriptName}, {@applied}, {@repeatable})
            END ";
    }

    protected override string GetJournalEntriesSql()
    {
        return @$"DECLARE @columnVariable AS NVARCHAR(max)

            SELECT @columnVariable =
            CASE WHEN EXISTS
            (
                SELECT 1 FROM sys.columns WHERE Name = N'Repeatable' AND Object_ID = Object_ID(N'dbo.Migration')
            )
            THEN
            (
                'where [Repeatable] = 0'
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
