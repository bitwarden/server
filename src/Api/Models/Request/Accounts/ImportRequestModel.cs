using System;
using System.Collections.Generic;

namespace Bit.Api.Models
{
    public class ImportRequestModel
    {
        public FolderRequestModel[] Folders { get; set; }
        public SiteRequestModel[] Sites { get; set; }
        public KeyValuePair<int, int>[] SiteRelationships { get; set; }
    }
}
