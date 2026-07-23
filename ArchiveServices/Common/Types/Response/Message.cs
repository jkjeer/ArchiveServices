using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Enterprise
{
    [CollectionDataContract(Namespace = "")]
    public class Messages : List<Message>
    {
    }

    [DataContract(Namespace = "")]
    public class Message
    {
        public Message()
        {
        }

        public Message(string name, string value)
        {
            Name = name;
            Value = value;
        }

        [DataMember(Order = 10)]
        public string Name { get; set; }
        [DataMember(Order = 20)]
        public string Value { get; set; }
    }
}
