using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.SubscriptionSvc;

namespace ArchiveServices.Common.Types.Enterprise
{
    [CollectionDataContract(Namespace = "")]
    public class Subscriptions : List<Subscription>
    {
    }

    [DataContract(Namespace = "")]
    public class Subscription
    {
        [DataMember(Order = 10)]
        public int Id { get; set; }
        [DataMember(Order = 12)]
        public string Name { get; set; }

        [DataMember(Order = 16)]
        public Plant Plant { get; set; }

        [DataMember(Order = 20)]
        public SubscriptionType SubscriptionType { get; set; }

        [DataMember(Order = 50)]
        public bool IsActive { get; set; }
        [DataMember(Order = 50)]
        public bool IsDisabled { get; set; }
        [DataMember(Order = 50)]
        public bool IsRunning { get; set; }
    }
}
