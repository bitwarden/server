namespace Bit.DataBoundaries.Schema;

internal sealed record SchemaParseError(string SchemaFile, int Line, int Column, string Message);
