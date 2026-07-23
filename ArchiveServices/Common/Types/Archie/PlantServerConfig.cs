using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Response;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class PlantServerConfigs : List<PlantServerConfig>
    {
    }

    [DataContract(Namespace = "")]

    public class PlantServerConfig
    {
        public PlantServerConfig()
        {
            SelectCommand = "";
            LastRunStatus = "Error";
        }

        [DataMember(Order = 10)]
        public int ID { get; set; }
        [DataMember(Order = 20)]
        public string PlantCode { get; set; }
        [DataMember(Order = 40)]
        public string ServerName { get; set; }

        [DataMember(Order = 30)]
        public bool IsActive { get; set; }

        [DataMember(Order = 31)]
        public bool IsEnabled { get; set; }

        [DataMember(Order = 162)]
        public int SFDays { get; set; }

        [DataMember(Order = 300)]
        public string LastRunStatus { get; set; }
        [DataMember(Order = 310)]
        public DateTime LastRunStartDate { get; set; }
        [DataMember(Order = 311)]
        public DateTime LastRunEndDate { get; set; }

        [DataMember(Order = 311)]
        public DateTime NextRunDate { get; set; }

        [DataMember(Order = 312)]
        public int ProcessID { get; set; }

        [DataMember(Order = 600)]
        public Guid Guid { get; set; }

        [DataMember(Order = 700)]
        public string SelectCommand { get; set; }

        [DataMember(Order = 800)]
        public ValidationErrors ValidationErrors { get; set; }
        [DataMember(Order = 810, EmitDefaultValue = false)]
        public string ValidationStatus { get; set; }

        [DataMember(Order = 300)]
        public string CreatedBy { get; set; }
        [DataMember(Order = 300)]
        public string LastEditBy { get; set; }
    }
}
