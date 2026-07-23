using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
// ReSharper disable InconsistentNaming

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class Servers : List<Server>
    {
    }

    [DataContract(Namespace = "")]

    public class Server
    {
        [DataMember(Order = 10)]
        public string ServerName { get; set; }
        [DataMember(Order = 20)]
        public string NetBiosName { get; set; }
        [DataMember(Order = 30)]
        public string WorkloadType { get; set; }
        [DataMember(Order = 40)]
        public string ServerFunction { get; set; }
        [DataMember(Order = 50)]
        public string DeploymentMode { get; set; }
        [DataMember(Order = 60)]
        public string ServerType { get; set; }
        [DataMember(Order = 70)]
        public bool IsActive { get; set; }
        [DataMember(Order = 70)]
        public bool IsEnabled { get; set; }
        [DataMember(Order = 70)]
        public bool IsSQL { get; set; }
        [DataMember(Order = 70)]
        public bool DeleteLogFiles { get; set; }
        [DataMember(Order = 70)]
        public int LogFileRetentionDays { get; set; }

        [DataMember(Order = 210)]
        public ValidationErrors ValidationErrors { get; set; }
        [DataMember(Order = 220, EmitDefaultValue = false)]
        public string ValidationStatus { get; set; }

        [DataMember(Order = 300, EmitDefaultValue = false)]
        public Plant Plant { get; set; }
    }
}
