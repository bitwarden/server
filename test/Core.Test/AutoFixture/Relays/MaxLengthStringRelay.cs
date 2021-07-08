using AutoFixture.Kernel;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace Bit.Core.Test.AutoFixture.Relays
{
    // Creates a string the same length as any availible MaxLength data annotation
    // Modified version of the StringLenfthRelay provided by AutoFixture
    // https://github.com/AutoFixture/AutoFixture/blob/master/Src/AutoFixture/DataAnnotations/StringLengthAttributeRelay.cs
    internal class MaxLengthStringRelay: ISpecimenBuilder
    {
        public object Create(object request, ISpecimenContext context)
        {
            if (request == null)
            {
                return new NoSpecimen();
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var p = request as PropertyInfo;
            if (p == null)
            {
                return new NoSpecimen();
            }

            var a = (MaxLengthAttribute)p.GetCustomAttributes(typeof(MaxLengthAttribute), false).SingleOrDefault();

            if (a == null)
            {
                return new NoSpecimen();
            }

            return context.Resolve(new ConstrainedStringRequest(a.Length, a.Length));
        }
    }
}

