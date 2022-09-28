using System.Text.Json;
using Bit.Api.Models.Request;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Bit.Api.Utilities;

public static class MultipartFormDataHelper
{
    private static readonly FormOptions _defaultFormOptions = new FormOptions();

    public static async Task GetFileAsync(this HttpRequest request, Func<Stream, string, string, Task> callback)
    {
        var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType),
            _defaultFormOptions.MultipartBoundaryLengthLimit);
        var reader = new MultipartReader(boundary, request.Body);

        var firstSection = await reader.ReadNextSectionAsync();
        if (firstSection != null)
        {
            if (ContentDispositionHeaderValue.TryParse(firstSection.ContentDisposition, out var firstContent))
            {
                if (HasFileContentDisposition(firstContent))
                {
                    // Old style with just data
                    var fileName = HeaderUtilities.RemoveQuotes(firstContent.FileName).ToString();
                    using (firstSection.Body)
                    {
                        await callback(firstSection.Body, fileName, null);
                    }
                }
                else if (HasDispositionName(firstContent, "key"))
                {
                    // New style with key, then data
                    string key = null;
                    using (var sr = new StreamReader(firstSection.Body))
                    {
                        key = await sr.ReadToEndAsync();
                    }

                    var secondSection = await reader.ReadNextSectionAsync();
                    if (secondSection != null)
                    {
                        if (ContentDispositionHeaderValue.TryParse(secondSection.ContentDisposition,
                            out var secondContent) && HasFileContentDisposition(secondContent))
                        {
                            var fileName = HeaderUtilities.RemoveQuotes(secondContent.FileName).ToString();
                            using (secondSection.Body)
                            {
                                await callback(secondSection.Body, fileName, key);
                            }
                        }

                        secondSection = null;
                    }
                }
            }

            firstSection = null;
        }
    }

    public static async Task GetSendFileAsync(this HttpRequest request, Func<Stream, string,
        SendRequestModel, Task> callback)
    {
        var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType),
            _defaultFormOptions.MultipartBoundaryLengthLimit);
        var reader = new MultipartReader(boundary, request.Body);

        var firstSection = await reader.ReadNextSectionAsync();
        if (firstSection != null)
        {
            if (ContentDispositionHeaderValue.TryParse(firstSection.ContentDisposition, out _))
            {
                var secondSection = await reader.ReadNextSectionAsync();
                if (secondSection != null)
                {
                    if (ContentDispositionHeaderValue.TryParse(secondSection.ContentDisposition,
                        out var secondContent) && HasFileContentDisposition(secondContent))
                    {
                        var fileName = HeaderUtilities.RemoveQuotes(secondContent.FileName).ToString();
                        using (secondSection.Body)
                        {
                            var model = await JsonSerializer.DeserializeAsync<SendRequestModel>(firstSection.Body);
                            await callback(secondSection.Body, fileName, model);
                        }
                    }

                    secondSection = null;
                }

            }

            firstSection = null;
        }
    }

    public static async Task GetFileAsync(this HttpRequest request, Func<Stream, Task> callback)
    {
        var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType),
            _defaultFormOptions.MultipartBoundaryLengthLimit);
        var reader = new MultipartReader(boundary, request.Body);

        var dataSection = await reader.ReadNextSectionAsync();
        if (dataSection != null)
        {
            if (ContentDispositionHeaderValue.TryParse(dataSection.ContentDisposition, out var dataContent)
                && HasFileContentDisposition(dataContent))
            {
                using (dataSection.Body)
                {
                    await callback(dataSection.Body);
                }
            }
            dataSection = null;
        }
    }


    private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
    {
        var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
        if (StringSegment.IsNullOrEmpty(boundary))
        {
            throw new InvalidDataException("Missing content-type boundary.");
        }

        if (boundary.Length > lengthLimit)
        {
            throw new InvalidDataException($"Multipart boundary length limit {lengthLimit} exceeded.");
        }

        return boundary.ToString();
    }

    private static bool HasFileContentDisposition(ContentDispositionHeaderValue content)
    {
        // Content-Disposition: form-data; name="data"; filename="Misc 002.jpg"
        return content != null && content.DispositionType.Equals("form-data") &&
            (!StringSegment.IsNullOrEmpty(content.FileName) || !StringSegment.IsNullOrEmpty(content.FileNameStar));
    }

    private static bool HasDispositionName(ContentDispositionHeaderValue content, string name)
    {
        // Content-Disposition: form-data; name="key";
        return content != null && content.DispositionType.Equals("form-data") && content.Name == name;
    }
}
