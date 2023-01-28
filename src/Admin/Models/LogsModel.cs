using Serilog.Events;

namespace Bit.Admin.Models;

public class LogsModel : CursorPagedModel<LogModel>
{
    public LogEventLevel? Level { get; set; }
    public string Project { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
}
