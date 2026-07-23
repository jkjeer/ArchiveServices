using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Response;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class LedgerTableCandidateConfigs : List<LedgerTableCandidateConfig>
    {
    }

    [DataContract(Namespace = "")]

    public class LedgerTableCandidateConfig
    {

        public LedgerTableCandidateConfig()
        {
            LastRunEndDate = DateTime.Now;
            LastRunStatus = "Error";
            ValidationErrors = new ValidationErrors();
        }

        [DataMember(Order = 1)]
        public bool IsTruncate { get; set; }
        [DataMember(Order = 4)]
        public bool StatsOnly { get; set; }

        [DataMember(Order = 10)]
        public long ConfigID { get; set; }
        [DataMember(Order = 20)]
        public long BatchID { get; set; }
        [DataMember(Order = 30)]
        public int ProcessID { get; set; }

        [DataMember(Order = 40)]
        public string GroupName { get; set; }
        [DataMember(Order = 40)]
        public string PlantCode { get; set; }

        [DataMember(Order = 40)]
        public string SourceServerName { get; set; }
        [DataMember(Order = 50)]
        public string SourceDatabaseName { get; set; }
        [DataMember(Order = 60)]
        public string SourceSchemaName { get; set; }
        [DataMember(Order = 70)]
        public string SourceTableName { get; set; }

        [DataMember(Order = 80)]
        public string FullyQualifiedSourceName
        {
            get { return $@"[{SourceDatabaseName}].[{SourceSchemaName}].[{SourceTableName}]"; }
            set { }
        }

        [DataMember(Order = 80)]
        public string FullyQualifiedSourceSchemaName
        {
            get { return $@"[{SourceDatabaseName}].[{SourceSchemaName}]"; }
            set { }
        }

        [DataMember(Order = 85)]
        public string ServerQualifiedSourceDatabaseName
        {
            get { return $@"[{SourceServerName}].[{SourceDatabaseName}]"; }
            set { }
        }

        [DataMember(Order = 85)]
        public string ServerQualifiedSourceName
        {
            get { return $@"[{SourceServerName}].[{SourceDatabaseName}].[{SourceSchemaName}].[{SourceTableName}]"; }
            set { }
        }

        [DataMember(Order = 85)]
        public string ServerQualifiedSourceSchemaName
        {
            get { return $@"[{SourceServerName}].[{SourceDatabaseName}].[{SourceSchemaName}]"; }
            set { }
        }

        [DataMember(Order = 100)]
        public string DateColumn { get; set; }
        [DataMember(Order = 100)]
        public bool IsActive { get; set; }
        [DataMember(Order = 110)]
        public bool IsEnabled { get; set; }
        [DataMember(Order = 120)]
        public bool IsTestMode { get; set; }
        [DataMember(Order = 121)]
        public bool TestModeConfig { get; set; }
        [DataMember(Order = 121)]
        public bool AutoConfigDisable { get; set; }
        [DataMember(Order = 121)]
        public bool IsAutoDisable { get; set; }

        [DataMember(Order = 127)]
        public bool IsRunning { get; set; }

        [DataMember(Order = 130)]
        public bool TestModeNoDelete { get; set; }

        [DataMember(Order = 165)]
        public long RowsRemaining { get; set; }

        [DataMember(Order = 170)]
        public int SourceDays { get; set; }

        [DataMember(Order = 184)]
        public string SelectCommand { get; set; }

        [DataMember(Order = 190)]
        public long RunFrequencyDays { get; set; }

        [DataMember(Order = 195)]
        public DateTime NextRunDate { get; set; }

        [DataMember(Order = 250)]
        public string Statement { get; set; }

        [DataMember(Order = 250)]
        public string TruncateCommand { get; set; }

        public DateTime TruncateStartTime { get; set; }
        [DataMember(Order = 250)]
        public DateTime TruncateEndTime { get; set; }
        [DataMember(Order = 250)]
        public decimal TruncateSeconds
        {
            get { return Convert.ToDecimal((TruncateEndTime - TruncateStartTime).TotalSeconds); }
            set { }
        }

        [DataMember(Order = 270)]
        public DateTime LastRunStartDate { get; set; }
        [DataMember(Order = 271)]
        public DateTime LastRunEndDate { get; set; }

        [DataMember(Order = 300)]
        public string LastRunStatus { get; set; }

        [DataMember(Order = 320)]
        public long SourceInitialRowCount { get; set; }

        [DataMember(Order = 330)]
        public long SourceFinalRowCount { get; set; }

        [DataMember(Order = 340)]
        public long RowsTruncated { get; set; }
        [DataMember(Order = 340)]
        public long RowsAffected { get; set; }
        [DataMember(Order = 340)]
        public long SelectedRows { get; set; }

        [DataMember(Order = 3100)]
        public long SourceRowCount { get; set; }
        [DataMember(Order = 3110)]
        public DateTime SourceMinDate { get; set; }
        [DataMember(Order = 3111)]
        public DateTime SourceMaxDate { get; set; }
        [DataMember(Order = 3112)]
        public string SourceMaxDateString { get; set; }

        [DataMember(Order = 800)]
        public ValidationErrors ValidationErrors { get; set; }
        [DataMember(Order = 810, EmitDefaultValue = false)]
        public string ValidationStatus { get; set; }

        [DataMember(Order = 900)]
        public string CreatedBy { get; set; }
        [DataMember(Order = 910)]
        public string LastEditBy { get; set; }
    }
}
