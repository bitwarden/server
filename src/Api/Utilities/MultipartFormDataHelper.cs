using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Bit.Api.Utilities
{
    public static class MultipartFormDataHelper
    {
        private static readonly FormOptions _defaultFormOptions = new FormOptions();

        public static async Task GetFilesAsync(this HttpRequest request, Func<Stream, string, Task> callback)
        {
            var boundary = GetBoundary(MediaTypeHeaderValue.Parse(request.ContentType),
                _defaultFormOptions.MultipartBoundaryLengthLimit);
            var reader = new MultipartReader(boundary, request.Body);

            var section = await reader.ReadNextSectionAsync();
            while(section != null)
            {
                ContentDispositionHeaderValue content;
                if(ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out content) &&
                    HasFileContentDisposition(content))
                {
                    await callback(section.Body, HeaderUtilities.RemoveQuotes(content.FileName));
                }

                section = await reader.ReadNextSectionAsync();
            }
        }

        private static string GetBoundary(MediaTypeHeaderValue contentType, int lengthLimit)
        {
            var boundary = HeaderUtilities.RemoveQuotes(contentType.Boundary);
            if(string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidDataException("Missing content-type boundary.");
            }

            if(boundary.Length > lengthLimit)
            {
                throw new InvalidDataException($"Multipart boundary length limit {lengthLimit} exceeded.");
            }

            return boundary;
        }

        private static bool HasFileContentDisposition(ContentDispositionHeaderValue content)
        {
            // Content-Disposition: form-data; name="myfile1"; filename="Misc 002.jpg"
            return content != null && content.DispositionType.Equals("form-data") &&
                (!string.IsNullOrEmpty(content.FileName) || !string.IsNullOrEmpty(content.FileNameStar));
        }
    }
}
