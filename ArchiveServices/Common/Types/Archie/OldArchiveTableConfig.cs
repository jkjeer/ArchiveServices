using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Managers.Enterprise;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class OldArchiveTableConfigs : List<OldArchiveTableConfig>
    {
    }

    [DataContract(Namespace = "")]

    public class OldArchiveTableConfig
    {

        public OldArchiveTableConfig()
        {
            SourceKeyColumns = new KeyColumns();
            SelectCommand = "";
            DeleteCommand = "";
            LastRunStatus = "Error";
            BulkCopySeconds = 0;
            VerifySeconds = 0;
            DeleteSeconds = 0;
            MovedRows = 0;
        }

        [DataMember(Order = 10)]
        public int ID { get; set; }
        [DataMember(Order = 40)]
        public string ServerName { get; set; }
        [DataMember(Order = 50)]
        public string DatabaseName { get; set; }
        [DataMember(Order = 60)]
        public string SchemaName { get; set; }
        [DataMember(Order = 70)]
        public string TableName { get; set; }

        [DataMember(Order = 80)]
        public string FullyQualifiedName 
        { 
            get
            { return $@"[{DatabaseName}].[{SchemaName}].[{TableName}]"; }
            set 
            { }
        }

        [DataMember(Order = 85)]
        public string ServerQualifiedName 
        { 
            get
            { return $@"[{ServerName}].[{DatabaseName}].[{SchemaName}].[{TableName}]"; }
            set
            { }
        }

        [DataMember(Order = 90)]
        public string RepoDatabaseName
        {
            get
            { return $"{ServerName}_{DatabaseName}_Archive"; }
            set
            { }
        }

        [DataMember(Order = 100)]
        public bool IsActive { get; set; }
        [DataMember(Order = 110)]
        public bool IsEnabled { get; set; }
        [DataMember(Order = 120)]
        public bool IsTestMode { get; set; }
        [DataMember(Order = 120)]
        public bool IsHammerMode { get; set; }
        [DataMember(Order = 120)]
        public bool IsRunning { get; set; }

        [DataMember(Order = 130)]
        public int MaxDeleteRowsPerBatch { get; set; }
        [DataMember(Order = 130)]
        public bool TestModeNoDelete { get; set; }

        [DataMember(Order = 130)]
        public bool TruncateRepoLedgerAfter { get; set; }

        [DataMember(Order = 140)]
        public string KeyColumn { get; set; }
        [DataMember(Order = 141)]
        public KeyColumns SourceKeyColumns { get; set; }
        [DataMember(Order = 142)]
        public bool IsKeyColumnUnique { get; set; }
        [DataMember(Order = 150)]
        public string DateColumn { get; set; }

        [DataMember(Order = 160)]
        public int RetentionDays { get; set; }
        [DataMember(Order = 170)]
        public DateTime ArchiveStartTime
        {
            get
            { return DateTime.Now.AddDays(-RetentionDays); }
            set
            { }
        }
        
        [DataMember(Order = 162)]
        public int SFDays { get; set; }


        [DataMember(Order = 180)]
        public string OtherCondition { get; set; }

        [DataMember(Order = 200)]
        public string WhereClause
        {
            get
            { return string.IsNullOrEmpty(OtherCondition) ? $@"WHERE [{DateColumn}] < '{ArchiveStartTime}' AND [{DateColumn}] > '2010-01-01'" : $@"WHERE [{DateColumn}] < '{ArchiveStartTime}' AND [{DateColumn}] > '2010-01-01' AND {OtherCondition}"; }
            set
            { }
        }

        [DataMember(Order = 300)]
        public string LastRunStatus { get; set; }
        [DataMember(Order = 310)]
        public DateTime LastArchiveStartDate { get; set; }
        [DataMember(Order = 311)]
        public DateTime LastArchiveEndDate { get; set; }

        [DataMember(Order = 311)]
        public DateTime NextRunDate { get; set; }

        [DataMember(Order = 312)]
        public int ProcessID { get; set; }

        [DataMember(Order = 320)]
        public long InitialRowCount { get; set; }

        [DataMember(Order = 330)]
        public long RowsToDelete { get; set; }

        [DataMember(Order = 500)]
        public long FinalRowCountCalculated { get; set; }

        [DataMember(Order = 500)]
        public long FinalRowCountActual { get; set; }

        [DataMember(Order = 3110)]
        public DateTime SFMinDate { get; set; }
        [DataMember(Order = 3111)]
        public DateTime SFMaxDate { get; set; }


        [DataMember(Order = 340)]
        public long SelectedRows { get; set; }
        [DataMember(Order = 350)]
        public long TempRows { get; set; }
        [DataMember(Order = 360)]
        public long MovedRows { get; set; }
        [DataMember(Order = 361)]
        public decimal BulkCopySeconds { get; set; }

        [DataMember(Order = 370)]
        public long VerifiedRows { get; set; }
        [DataMember(Order = 361)]
        public decimal VerifySeconds { get; set; }

        [DataMember(Order = 370)]
        public long MissingKeys { get; set; }
        [DataMember(Order = 361)]
        public decimal MissingKeysSeconds { get; set; }

        [DataMember(Order = 380)]
        public long DeletedRows { get; set; }
        [DataMember(Order = 381)]
        public decimal DeleteSeconds { get; set; }

        [DataMember(Order = 400)]
        public long TotalSelectedRows { get; set; }
        [DataMember(Order = 410)]
        public long TotalTempRows { get; set; }
        [DataMember(Order = 420)]
        public long TotalMovedRows { get; set; }
        [DataMember(Order = 430)]
        public long TotalDeletedRows { get; set; }

        [DataMember(Order = 600)]
        public Guid Guid { get; set; }

        [DataMember(Order = 700)]
        public string SelectCommand { get; set; }
        [DataMember(Order = 710)]
        public string DeleteCommand { get; set; }

        [DataMember(Order = 800)]
        public ValidationErrors ValidationErrors { get; set; }
        [DataMember(Order = 810, EmitDefaultValue = false)]
        public string ValidationStatus { get; set; }

        [DataMember(Order = 900)]
        public string CreatedBy { get; set; }
        [DataMember(Order = 910)]
        public string LastEditBy { get; set; }

        [DataMember(Order = 1000)]
        public string ConstraintTypeCode { get; set; }
        [DataMember(Order = 1010)]
        public string ConstraintTypeDesc { get; set; }
        [DataMember(Order = 1020)]
        public string ConstraintName { get; set; }
        [DataMember(Order = 1030)]
        public string ReferencedSchemaName { get; set; }
        [DataMember(Order = 1040)]
        public string ReferencedTableName { get; set; }
        [DataMember(Order = 1050)]
        public string ReferencedColumnName { get; set; }

    }
}
