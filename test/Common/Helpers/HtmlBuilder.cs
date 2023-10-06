using System.Text;

namespace Bit.Test.Common.Helpers;

public class HtmlBuilder
{
    private string _topLevelNode;
    private readonly StringBuilder _builder = new();

    public HtmlBuilder(string topLevelNode = "html")
    {
        _topLevelNode = CoerceTopLevelNode(topLevelNode);
    }

    public HtmlBuilder Append(string node)
    {
        _builder.Append(node);
        return this;
    }

    public HtmlBuilder Append(HtmlBuilder builder)
    {
        _builder.Append(builder.ToString());
        return this;
    }

    public HtmlBuilder WithAttribute(string name, string value)
    {
        _topLevelNode = $"{_topLevelNode} {name}=\"{value}\"";
        return this;
    }

    public override string ToString()
    {
        _builder.Insert(0, $"<{_topLevelNode}>");
        _builder.Append($"</{_topLevelNode}>");
        return _builder.ToString();
    }

    private static string CoerceTopLevelNode(string topLevelNode)
    {
        var result = topLevelNode;
        if (topLevelNode.StartsWith("<"))
        {
            result = topLevelNode[1..];
        }
        if (topLevelNode.EndsWith(">"))
        {
            result = result[..^1];
        }

        if (topLevelNode.IndexOf(">") != -1)
        {
            throw new ArgumentException("Top level nodes cannot contain '>' characters.");
        }

        return result;
    }
}
