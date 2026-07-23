using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace BIServices.Common.Types.Enterprise
{

    [CollectionDataContract(Namespace = "")]
    public class RbaRacks : List<RbaRack>
    {
    }

    [DataContract(Namespace = "")]
    public class RbaRack
    {

        [DataMember(Order = 10)]
        public decimal RackNum { get; set; }

        [DataMember(Order = 12)]
        public decimal ScheduleNum { get; set; }

        [DataMember(Order = 14)]
        public decimal BoxNum { get; set; }

        [DataMember(Order = 15)]
        public string RackType { get; set; }

        [DataMember(Order = 16)]
        public string Status { get; set; }

        [DataMember(Order = 20)]
        public string ProductLineCode { get; set; }

        [DataMember(Order = 22)]
        public string PriorityCode { get; set; }

        [DataMember(Order = 30)]
        public DateTime TransactionDate { get; set; }

        [DataMember(Order = 50)]
        public int Units { get; set; }

        [DataMember(Order = 60)]
        public string IsRequired { get; set; }
    }
}
