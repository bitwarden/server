using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Bit.Core.Models.Table;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class SelectionReadOnlyRequestModel
    {
        [Required]
        public string Id { get; set; }
        public bool ReadOnly { get; set; }

        public SelectionReadOnly ToSelectionReadOnly()
        {
            return new SelectionReadOnly
            {
                Id = new Guid(Id),
                ReadOnly = ReadOnly
            };
        }
    }
}
