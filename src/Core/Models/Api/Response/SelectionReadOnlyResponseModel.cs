using System;
using Bit.Core.Models.Data;

namespace Bit.Core.Models.Api
{
    public class SelectionReadOnlyResponseModel
    {
        public SelectionReadOnlyResponseModel(SelectionReadOnly selection)
        {
            if (selection == null)
            {
                throw new ArgumentNullException(nameof(selection));
            }

            Id = selection.Id.ToString();
            ReadOnly = selection.ReadOnly;
            HidePasswords = selection.HidePasswords;
        }

        public string Id { get; set; }
        public bool ReadOnly { get; set; }
        public bool HidePasswords { get; set; }
    }
}
