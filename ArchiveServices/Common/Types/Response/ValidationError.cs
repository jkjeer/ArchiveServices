using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Response
{
    [CollectionDataContract(Namespace = "")]
    public class ValidationErrors : List<ValidationError>
    {
    }

    [DataContract(Namespace = "")]
    public class ValidationError
    {
        public ValidationError()
        {
        }

        public ValidationError(string message, string type, string severity, bool isFatal)
        {
            Message = message;
            Type = type;
            Severity = severity;
            IsFatal = isFatal;
        }

        [DataMember(Order = 10)]
        public string Message { get; set; }

        [DataMember(Order = 20)]
        public string Type { get; set; }

        [DataMember(Order = 30)]
        public string Severity { get; set; }

        [DataMember(Order = 40)]
        public bool IsFatal { get; set; }
    }
}
