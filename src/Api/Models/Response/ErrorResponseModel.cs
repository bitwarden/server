using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNet.Mvc.ModelBinding;

namespace Bit.Api.Models.Response
{
    public class ErrorResponseModel : ResponseModel
    {
        public ErrorResponseModel()
            : base("error")
        { }

        public ErrorResponseModel(ModelStateDictionary modelState)
            : this()
        {
            Message = "The model state is invalid.";
            ValidationErrors = new Dictionary<string, IEnumerable<string>>();

            var keys = modelState.Keys.ToList();
            var values = modelState.Values.ToList();

            for(var i = 0; i < values.Count; i++)
            {
                var value = values[i];

                if(keys.Count <= i)
                {
                    // Keys not available for some reason.
                    break;
                }

                var key = keys[i];

                if(value.ValidationState != ModelValidationState.Invalid || value.Errors.Count == 0)
                {
                    continue;
                }

                var errors = value.Errors.Select(e => e.ErrorMessage);
                ValidationErrors.Add(key, errors);
            }
        }

        public string Message { get; set; }
        public Dictionary<string, IEnumerable<string>> ValidationErrors { get; set; }
        // For use in development environments.
        public string ExceptionMessage { get; set; }
        public string ExceptionStackTrace { get; set; }
        public string InnerExceptionMessage { get; set; }
    }
}
