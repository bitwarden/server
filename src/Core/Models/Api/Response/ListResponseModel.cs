using System;
using System.Collections.Generic;

namespace Bit.Core.Models.Api
{
    public class ListResponseModel<T> : ResponseModel where T : ResponseModel
    {
        public ListResponseModel(IEnumerable<T> data, string continuationToken = null)
            : base("list")
        {
            Data = data;
            ContinuationToken = continuationToken;
        }

        public IEnumerable<T> Data { get; set; }
        public string ContinuationToken { get; set; }
    }
}
