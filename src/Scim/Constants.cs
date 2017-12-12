namespace Bit.Scim
{
    public class Constants
    {
        public static class Schemas
        {
            public const string User = @"urn:ietf:params:scim:schemas:core:2.0:User";
            public const string UserEnterprise = @"urn:ietf:params:scim:schemas:extension:enterprise:2.0:User";
            public const string Group = @"urn:ietf:params:scim:schemas:core:2.0:Group";
        }

        public static class Messages
        {
            public const string Error = @"urn:ietf:params:scim:api:messages:2.0:Error";
            public const string PatchOp = @"urn:ietf:params:scim:api:messages:2.0:PatchOp";
            public const string ListResponse = @"urn:ietf:params:scim:api:messages:2.0:ListResponse";
            public const string SearchRequest = @"urn:ietf:params:scim:api:messages:2.0:SearchRequest";
        }
    }
}
