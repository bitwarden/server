namespace Bit.Icons.Models;

public class IconResult
{
    public IconResult(string href, string sizes)
    {
        Path = href;
        if (!string.IsNullOrWhiteSpace(sizes))
        {
            var sizeParts = sizes.Split('x');
            if (sizeParts.Length == 2 && int.TryParse(sizeParts[0].Trim(), out var width) &&
                int.TryParse(sizeParts[1].Trim(), out var height))
            {
                DefinedWidth = width;
                DefinedHeight = height;

                if (width == height)
                {
                    if (width == 32)
                    {
                        Priority = 1;
                    }
                    else if (width == 64)
                    {
                        Priority = 2;
                    }
                    else if (width >= 24 && width <= 128)
                    {
                        Priority = 3;
                    }
                    else if (width == 16)
                    {
                        Priority = 4;
                    }
                    else
                    {
                        Priority = 100;
                    }
                }
            }
        }

        if (Priority == 0)
        {
            Priority = 200;
        }
    }

    public IconResult(Uri uri, byte[] bytes, string format)
    {
        Path = uri.ToString();
        Icon = new Icon
        {
            Image = bytes,
            Format = format
        };
        Priority = 10;
    }

    public string Path { get; set; }
    public int? DefinedWidth { get; set; }
    public int? DefinedHeight { get; set; }
    public Icon Icon { get; set; }
    public int Priority { get; set; }
}
