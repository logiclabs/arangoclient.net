namespace ArangoDB.Client
{
    public static class ArangoAttributes
    {
        public const string Id = "_id";
        public const string Key = "_key";
        public const string Revision = "_rev";

        public const string EdgeFrom = "_from";
        public const string EdgeTo = "_to";

        public static bool IsSystemAttribute(string name)
        {
            return IsDocumentSystemAttribute(name) || IsEdgeSystemAttribute(name);
        }

        public static bool IsDocumentSystemAttribute(string name)
        {
            return name == Id || name == Key || name == Revision;
        }

        public static bool IsEdgeSystemAttribute(string name)
        {
            return name == EdgeFrom || name == EdgeTo;
        }

        public static IdentifierType[] GetAllIdentifierTypes()
        {
            return new[]
            {
                IdentifierType.Key,
                IdentifierType.Handle,
                IdentifierType.Revision,
                IdentifierType.EdgeFrom,
                IdentifierType.EdgeTo
            };
        }

        public static string ToAttributeName(this IdentifierType identifierType)
        {
            switch (identifierType)
            {
                case IdentifierType.None:
                    return null;
                case IdentifierType.Key:
                    return ArangoAttributes.Key;
                case IdentifierType.Handle:
                    return ArangoAttributes.Id;
                case IdentifierType.Revision:
                    return ArangoAttributes.Revision;
                case IdentifierType.EdgeFrom:
                    return ArangoAttributes.EdgeFrom;
                case IdentifierType.EdgeTo:
                    return ArangoAttributes.EdgeTo;
                default:
                    return null;
            }
        }
 
    }
}
