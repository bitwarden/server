using System;
using System.Collections.Generic;

namespace Bit.Api.Models
{
    public class ImportRequestModel
    {
        private LoginRequestModel[] _logins;

        public FolderRequestModel[] Folders { get; set; }
        [Obsolete]
        public LoginRequestModel[] Sites
        {
            get { return _logins; }
            set { _logins = value; }
        }
        public LoginRequestModel[] Logins
        {
            get { return _logins; }
            set { _logins = value; }
        }
        public KeyValuePair<int, int>[] FolderRelationships { get; set; }
    }
}
