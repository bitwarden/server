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

    protected new IDbCommand GetInsertScriptCommand(Func<IDbCommand> dbCommandFactory, SqlScript script)
    {
        var command = dbCommandFactory();

        var scriptNameParam = command.CreateParameter();
        scriptNameParam.ParameterName = "scriptName";
        scriptNameParam.Value = script.Name;
        command.Parameters.Add(scriptNameParam);

        var appliedParam = command.CreateParameter();
        appliedParam.ParameterName = "applied";
        appliedParam.Value = DateTime.Now;
        command.Parameters.Add(appliedParam);

        var rerunableParam = command.CreateParameter();
        rerunableParam.ParameterName = "rerunable";
        rerunableParam.Value = Rerunable;
        command.Parameters.Add(rerunableParam);

        command.CommandText = GetInsertJournalEntrySql("@scriptName", "@applied", "@rerunable");
        command.CommandType = CommandType.Text;
        return command;
    }

    protected string GetInsertJournalEntrySql(string @scriptName, string @applied, string @rerunable)
    {
        return $"IF EXISTS (SELECT * FROM {FqSchemaTableName} WHERE Rerunable = 1 AND ScriptName ='{@scriptName}') " +
        "BEGIN " +
        $"UPDATE {FqSchemaTableName} SET Applied = {@applied}, Rerunable = {@rerunable} WHERE ScriptName = '{@scriptName}' " +
        "END " +
        "ELSE " +
        "BEGIN " +
        $"insert into {FqSchemaTableName} (ScriptName, Applied, Rerunable) values ({@scriptName}, {@applied}, {@rerunable}) " +
        "END ";
    }

    protected override string GetJournalEntriesSql()
    {
        return @$"DECLARE @columnVariable AS NVARCHAR(max)
            DECLARE @SQLString AS NVARCHAR(max)


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
            ('')
            END

            PRINT @columnVariable

            SET @SQLString = N'select [ScriptName] from dbo.Migration ' + @columnVariable + ' order by [ScriptName]'

            PRINT @SQLString

            EXECUTE sp_executesql @SQLString";
    }
}
