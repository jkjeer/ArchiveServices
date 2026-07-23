using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
// ReSharper disable InconsistentNaming

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class BackupTableConfigs : List<BackupTableConfig>
    {
    }

    [DataContract(Namespace = "")]

    public class BackupTableConfig
    {
        [DataMember(Order = 10)]
        public string TableName { get; set; }
        [DataMember(Order = 11)]
        public string SchemaName { get; set; }
        [DataMember(Order = 12)]
        public string DatabaseName { get; set; }
        [DataMember(Order = 13)]
        public string ServerName { get; set; }
        [DataMember(Order = 14)]
        public string PlantCode { get; set; }

        [DataMember(Order = 15)]
        public string QualifiedName => $@"[{ServerName}].[{DatabaseName}].[{SchemaName}].[{TableName}]";

        [DataMember(Order = 20)]
        public int ID { get; set; }

        [DataMember(Order = 30)]
        public bool IsActive { get; set; }
        [DataMember(Order = 31)]
        public bool IsEnabled { get; set; }
        [DataMember(Order = 32)]
        public int LastRunStatus { get; set; }
        [DataMember(Order = 33)]
        public string HostName { get; set; }
        [DataMember(Order = 34)]
        public DateTime LastBackupDate { get; set; }
        [DataMember(Order = 35)]
        public DateTime NextBackupDate { get; set; }
        [DataMember(Order = 36)]
        public bool BackupSF { get; set; }
        [DataMember(Order = 36)]
        public string DateColumn { get; set; }
        [DataMember(Order = 37)]
        public int RetentionDays { get; set; }
        [DataMember(Order = 37)]
        public int BackupFrequencyDays { get; set; }
        [DataMember(Order = 38)]
        public long RowsToDelete { get; set; }
        [DataMember(Order = 39)]
        public long DeletedRows { get; set; }

        [DataMember(Order = 40)]
        public Guid Guid { get; set; }

        [DataMember(Order = 50)]
        public string Command { get; set; }

        [DataMember(Order = 230)]
        public ValidationErrors ValidationErrors { get; set; }
        [DataMember(Order = 60, EmitDefaultValue = false)]
        public string ValidationStatus { get; set; }

        [DataMember(Order = 100, EmitDefaultValue = false)]
        public Plant Plant { get; set; }
    }
}
