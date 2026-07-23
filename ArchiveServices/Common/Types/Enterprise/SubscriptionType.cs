using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace ArchiveServices.SubscriptionSvc
{
    [CollectionDataContract(Namespace = "")]
    public class SubscriptionTypes : List<SubscriptionType>
    {
    }

    [DataContract(Namespace = "")]
    public class SubscriptionType
    {
        [DataMember(Order = 10)]
        public string Plant { get; set; }
    }
}
