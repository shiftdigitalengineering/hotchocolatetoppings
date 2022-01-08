using System;

namespace ShiftDigital.HotChocolate.Stitching.Dapr.Exception
{
    [Serializable]
    public class UnableToPublishSchemaException : System.Exception
    {
        public UnableToPublishSchemaException() { }

        public UnableToPublishSchemaException(string message)
        : base(message) { }

        public UnableToPublishSchemaException(string message, System.Exception innerException)
        : base(message, innerException) { }
    }
}
