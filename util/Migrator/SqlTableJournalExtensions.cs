using System.Data;
using DbUp.Builder;
using DbUp.Engine;
using DbUp.Engine.Output;
using DbUp.Engine.Transactions;
using DbUp.SqlServer;
using DbUp.Support;

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

        command.CommandText = GetInsertJournalEntrySql("@scriptName", "@applied", "@rerrunable");
        command.CommandType = CommandType.Text;
        return command;
    }

    protected override string GetInsertJournalEntrySql(string @scriptName, string @applied, string @rerrunable)
    {
        return $"insert into {FqSchemaTableName} (ScriptName, Applied, Rerunable) values ({@scriptName}, {@applied}, {@rerrunable})";
    }

    protected override string GetJournalEntriesSql()
    {
        return $"select [ScriptName] from {FqSchemaTableName} where [Rerunable] = 0 order by [ScriptName]";
    }

    protected override string CreateSchemaTableSql(string quotedPrimaryKeyName)
    {
        return
$@"create table {FqSchemaTableName} (
    [Id] int identity(1,1) not null constraint {quotedPrimaryKeyName} primary key,
    [ScriptName] nvarchar(255) not null,
    [Applied] datetime not null,
    [Rerunable] bit not null DEFAULT 0
)";
    }
}
