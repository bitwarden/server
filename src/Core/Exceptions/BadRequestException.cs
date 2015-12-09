using System;
using Microsoft.AspNet.Mvc.ModelBinding;

namespace Bit.Core.Exceptions
{
    public class BadRequestException : Exception
    {
        public BadRequestException(string message) : this(string.Empty, message) { }

        public BadRequestException(string key, string errorMessage)
            : base("The model state is invalid.")
        {
            ModelState = new ModelStateDictionary();
            ModelState.AddModelError(key, errorMessage);
        }

        public BadRequestException(ModelStateDictionary modelState)
            : base("The model state is invalid.")
        {
            if(modelState.IsValid || modelState.ErrorCount == 0)
            {
                return;
            }

            ModelState = modelState;
        }

        public ModelStateDictionary ModelState { get; set; }
    }
}
