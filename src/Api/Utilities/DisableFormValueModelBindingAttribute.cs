using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.Linq;

namespace Bit.Api.Utilities
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class DisableFormValueModelBindingAttribute : Attribute, IResourceFilter
    {
        public void OnResourceExecuting(ResourceExecutingContext context)
        {
            var formValue = context.ValueProviderFactories.OfType<FormValueProviderFactory>().FirstOrDefault();
            if(formValue != null)
            {
                context.ValueProviderFactories.Remove(formValue);
            }

            var jqFormValue = context.ValueProviderFactories.OfType<JQueryFormValueProviderFactory>().FirstOrDefault();
            if(jqFormValue != null)
            {
                context.ValueProviderFactories.Remove(jqFormValue);
            }
        }

        public void OnResourceExecuted(ResourceExecutedContext context)
        {
        }
    }
}
