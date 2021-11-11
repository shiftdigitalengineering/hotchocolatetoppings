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

    public class SchemaNameDto
    {
        public string Name { get; set; }

        public override bool Equals(object obj)
        {
            return this.Name.Equals(((SchemaNameDto)obj).Name, System.StringComparison.OrdinalIgnoreCase);
        }

        public override int GetHashCode()
        {
            int code = 0;
            if (!string.IsNullOrWhiteSpace(this.Name))
            {                
                foreach (char c in this.Name)
                {
                    code += System.Convert.ToInt32(c);
                }
            }
            return code;
        }

        public override string ToString()
        {
            return this.Name;

        }
    }
}
