using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Response;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class ArchiveTableConfigs : List<ArchiveTableConfig>
    {
    }

    [DataContract(Namespace = "")]

    public class ArchiveTableConfig
    {

        public ArchiveTableConfig()
        {
            SourceKeyColumns = new KeyColumns();
            DestinationKeyColumns = new KeyColumns();
            ExcludeColumns = new KeyColumns();
            LastRunEndDate = DateTime.Now;
            LastRunStatus = "Error";
            ValidationErrors = new ValidationErrors();
        }

        [DataMember(Order = 1)]
        public bool IsArchive { get; set; }
        [DataMember(Order = 2)]
        public bool IsTail { get; set; }
        [DataMember(Order = 3)]
        public bool IsVerify { get; set; }
        [DataMember(Order = 4)]
        public bool StatsOnly { get; set; }
        [DataMember(Order = 165)]
        public long MaxStatsRows { get; set; }

        [DataMember(Order = 10)]
        public long ConfigID { get; set; }
        [DataMember(Order = 20)]
        public long BatchID { get; set; }
        [DataMember(Order = 30)]
        public int ProcessID { get; set; }

        [DataMember(Order = 40)]
        public string SourceServerName { get; set; }
        [DataMember(Order = 50)]
        public string SourceDatabaseName { get; set; }
        [DataMember(Order = 60)]
        public string SourceSchemaName { get; set; }
        [DataMember(Order = 70)]
        public string SourceTableName { get; set; }
        [DataMember(Order = 50)]
        public string DestinationServerName { get; set; }
        [DataMember(Order = 50)]
        public string DestinationDatabaseName { get; set; }
        [DataMember(Order = 60)]
        public string DestinationSchemaName { get; set; }
        [DataMember(Order = 70)]
        public string DestinationTableName { get; set; }

        [DataMember(Order = 80)]
        public string FullyQualifiedSourceName
        {
            get { return $@"[{SourceDatabaseName}].[{SourceSchemaName}].[{SourceTableName}]"; }
            set { }
        }

        [DataMember(Order = 80)]
        public string FullyQualifiedDestinationName
        {
            get { return $@"[{DestinationDatabaseName}].[{DestinationSchemaName}].[{DestinationTableName}]"; }
            set { }
        }

        [DataMember(Order = 85)]
        public string ServerQualifiedSourceName
        {
            get { return $@"[{SourceServerName}].[{SourceDatabaseName}].[{SourceSchemaName}].[{SourceTableName}]"; }
            set { }
        }

        [DataMember(Order = 85)]
        public string ServerQualifiedDestinationName
        {
            get { return $@"[{DestinationServerName}].[{DestinationDatabaseName}].[{DestinationSchemaName}].[{DestinationTableName}]"; }
            set { }
        }

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

        [DataMember(Order = 126)]
        public bool IsHammerMode { get; set; }
        [DataMember(Order = 127)]
        public bool IsRunning { get; set; }

        [DataMember(Order = 130)]
        public bool TestModeNoDelete { get; set; }

        [DataMember(Order = 140)]
        public string SourceKey { get; set; }
        [DataMember(Order = 141)]
        public KeyColumns SourceKeyColumns { get; set; }
        [DataMember(Order = 142)]
        public bool IsSourceKeyUnique { get; set; }

        [DataMember(Order = 144)]
        public string DestinationKey { get; set; }
        [DataMember(Order = 145)]
        public KeyColumns DestinationKeyColumns { get; set; }
        [DataMember(Order = 146)]
        public bool IsDestinationKeyUnique { get; set; }

        [DataMember(Order = 150)]
        public string DateColumn { get; set; }

        [DataMember(Order = 160)]
        public DateTime SelectStartDate { get; set; }

        [DataMember(Order = 150)]
        public string IDColumn { get; set; }

        [DataMember(Order = 160)]
        public bool UseHighwaterMark { get; set; }
        [DataMember(Order = 161)]
        public DateTime OrigHighwaterMarkDate { get; set; }
        [DataMember(Order = 162)]
        public string OrigHighwaterMarkDateString { get; set; }

        [DataMember(Order = 161)]
        public DateTime HighwaterMarkDate { get; set; }
        [DataMember(Order = 162)]
        public string HighwaterMarkDateString { get; set; }

        [DataMember(Order = 163)]
        public Int64 HighwaterMarkID { get; set; }

        [DataMember(Order = 165)]
        public long RowsRemaining { get; set; }

        [DataMember(Order = 170)]
        public int SourceDays { get; set; }

        [DataMember(Order = 180)]
        public string OtherCondition { get; set; }

        [DataMember(Order = 184)]
        public string ExcludeColumn { get; set; }
        [DataMember(Order = 185)]
        public KeyColumns ExcludeColumns { get; set; }

        [DataMember(Order = 190)]
        public long RunFrequencyDays { get; set; }

        [DataMember(Order = 195)]
        public DateTime NextRunDate { get; set; }

        [DataMember(Order = 200)]
        public string WhereClause
        {
            get { return string.IsNullOrEmpty(OtherCondition) ? $@"WHERE [{DateColumn}] >= '{SelectStartDate}'" : $@"WHERE [{DateColumn}] >= '{SelectStartDate}' AND {OtherCondition}"; }
            set { }
        }

        [DataMember(Order = 250)]
        public string Statement { get; set; }

        [DataMember(Order = 270)]
        public DateTime LastRunStartDate { get; set; }
        [DataMember(Order = 271)]
        public DateTime LastRunEndDate { get; set; }

        [DataMember(Order = 300)]
        public string LastRunStatus { get; set; }


        [DataMember(Order = 311)]
        public long MaxRowsToMove { get; set; }

        [DataMember(Order = 320)]
        public long SourceInitialRowCount { get; set; }

        [DataMember(Order = 320)]
        public long DestinationInitialRowCount { get; set; }

        [DataMember(Order = 330)]
        public long SourceFinalRowCount { get; set; }

        [DataMember(Order = 3200)]
        public long DestinationFinalRowCount { get; set; }

        [DataMember(Order = 340)]
        public long RowsAffected { get; set; }

        [DataMember(Order = 3100)]
        public long SourceRowCount { get; set; }
        [DataMember(Order = 3110)]
        public DateTime SourceMinDate { get; set; }
        [DataMember(Order = 3111)]
        public DateTime SourceMaxDate { get; set; }
        [DataMember(Order = 3112)]
        public string SourceMaxDateString { get; set; }

        [DataMember(Order = 3210)]
        public DateTime DestinationMinDate { get; set; }
        [DataMember(Order = 3211)]
        public DateTime DestinationMaxDate { get; set; }
        [DataMember(Order = 3212)]
        public string DestinationMaxDateString { get; set; }

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
