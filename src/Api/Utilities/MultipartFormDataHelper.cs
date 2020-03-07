using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Bit.Api.Utilities
{
    public static class MultipartFormDataHelper
    {
        private static readonly FormOptions _defaultFormOptions = new FormOptions();

        public static async Task GetFileAsync(this HttpRequest request, Func<Stream, string, string, Task> callback)
        {
            var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType),
                _defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, request.Body);

            var firstSection = await reader.ReadNextSectionAsync();
            if(firstSection != null)
            {
                if(ContentDispositionHeaderValue.TryParse(firstSection.ContentDisposition, out var firstContent))
                {
                    if(HasFileContentDisposition(firstContent))
                    {
                        // Old style with just data
                        var fileName = HeaderUtilities.RemoveQuotes(firstContent.FileName).ToString();
                        using(firstSection.Body)
                        {
                            await callback(firstSection.Body, fileName, null);
                        }
                    }
                    else if(HasKeyDisposition(firstContent))
                    {
                        // New style with key, then data
                        string key = null;
                        using(var sr = new StreamReader(firstSection.Body))
                        {
                            key = await sr.ReadToEndAsync();
                        }

                        var secondSection = await reader.ReadNextSectionAsync();
                        if(secondSection != null)
                        {
                            if(ContentDispositionHeaderValue.TryParse(secondSection.ContentDisposition,
                                out var secondContent) && HasFileContentDisposition(secondContent))
                            {
                                var fileName = HeaderUtilities.RemoveQuotes(secondContent.FileName).ToString();
                                using(secondSection.Body)
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

        private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
            if(StringSegment.IsNullOrEmpty(boundary))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            if(boundary.Length > lengthLimit)
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

        private static bool HasKeyDisposition(ContentDispositionHeaderValue content)
        {
            // Content-Disposition: form-data; name="key";
            return content != null && content.DispositionType.Equals("form-data") && content.Name == "key";
        }
    }
}
