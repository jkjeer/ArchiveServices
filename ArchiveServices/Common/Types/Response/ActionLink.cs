using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Response
{
    [CollectionDataContract(Namespace = "")]
    public class ActionLinks : List<ActionLink>
    {
    }

    [DataContract(Namespace = "")]
    public class ActionLink
    {
        [DataMember(Order = 10)]
        public string Name { get; set; }
        [DataMember(Order = 20)]
        public string Description { get; set; }
        [DataMember(Order = 30)]
        public string Href { get; set; }

        public ActionLink()
        {
        }

        public ActionLink(string name, string desc, string href)
        {
            Name = name;
            Description = desc;
            Href = href;
        }
    }
}