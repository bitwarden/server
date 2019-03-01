using System;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api.Public
{
    public class AssociationWithPermissionsRequestModel
    {
        /// <summary>
        /// The associated object's unique identifier.
        /// </summary>
        /// <example>bfbc8338-e329-4dc0-b0c9-317c2ebf1a09</example>
        [Required]
        public Guid? Id { get; set; }
        /// <summary>
        /// When true, the read only permission will not allow the user or group to make changes to items.
        /// </summary>
        [Required]
        public bool? ReadOnly { get; set; }

        public SelectionReadOnly ToSelectionReadOnly()
        {
            return new SelectionReadOnly
            {
                Id = Id.Value,
                ReadOnly = ReadOnly.Value
            };
        }
    }
}
