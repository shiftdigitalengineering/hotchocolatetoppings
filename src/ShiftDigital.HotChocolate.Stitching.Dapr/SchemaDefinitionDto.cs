using System.Collections.Generic;

namespace Harmony.Data.Graphql.Stitching.Dapr
{
    internal sealed class SchemaDefinitionDto
    {
        public string? Name { get; set; }

        public string? Document { get; set; }

        public List<string> ExtensionDocuments { get; set; } = new List<string>();
    }

    public class SchemaNameDto
    {
        public string Name { get; set; }
    }
}
