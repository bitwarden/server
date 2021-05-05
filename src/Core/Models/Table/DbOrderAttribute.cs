using System;

namespace Bit.Core.Models.Table
{
    [AttributeUsage(AttributeTargets.Property, Inherited = false)]
    public class DbOrderAttribute : Attribute
    {
        public int ParameterOrder { get; private set; }

        public DbOrderAttribute(int parameterOrder)
        {
            ParameterOrder = parameterOrder;
        }
    }
}
