using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Harmony.Data.Graphql.Stitching.Dapr
{
    internal sealed class SchemaDefinitionDto
    {
        public string? Name { get; set; }

        public string? Document { get; set; }

        public List<string> ExtensionDocuments { get; set; } = new List<string>();
    }

    public class SchemaNameDto : IEqualityComparer<SchemaNameDto>
    {
        public string Name { get; set; }

        public bool Equals(SchemaNameDto x, SchemaNameDto y)
        {
            return x.Name.Equals(y.Name, System.StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode([DisallowNull] SchemaNameDto obj)
        {
            return obj.Name.GetHashCode();
        }
    }
}
