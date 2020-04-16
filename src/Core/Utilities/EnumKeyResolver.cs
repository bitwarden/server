using Newtonsoft.Json.Serialization;
using System;

namespace Bit.Core.Utilities
{
    public class EnumKeyResolver<T> : DefaultContractResolver where T : struct
    {
        protected override JsonDictionaryContract CreateDictionaryContract(Type objectType)
        {
            var contract = base.CreateDictionaryContract(objectType);
            var keyType = contract.DictionaryKeyType;

            if (keyType.BaseType == typeof(Enum))
            {
                contract.DictionaryKeyResolver = propName => ((T)Enum.Parse(keyType, propName)).ToString();
            }

            return contract;
        }
    }
}
