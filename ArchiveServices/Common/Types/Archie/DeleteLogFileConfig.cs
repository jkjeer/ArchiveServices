using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Response;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class DeleteLogFileConfigs : List<DeleteLogFileConfig>
    {
    }

    [DataContract(Namespace = "")]

    public class DeleteLogFileConfig
    {
        public DeleteLogFileConfig()
        {
            LastRunEndDate = DateTime.Now;
            LastRunStatus = "Error";
            ValidationErrors = new ValidationErrors();
        }

        public int ConfigID { get; set; }

        public string GroupName { get; set; }
        public string ServerName { get; set; }
        
        public string LogType { get; set; }
        public bool DeleteDirectories { get; set; }

        public string Path { get; set; }
        public string FileExt { get; set; }

        public string ServerQualifiedPathName
        {
            get { return $@"\\{ServerName}\{Path}\"; }
            set { }
        }

        public int LogRetentionDays { get; set; }
        public DateTime LogRetentionDate
        {
            get { return DateTime.Now.AddDays(-LogRetentionDays); }
            set { }
        }

        public bool IsActive { get; set; }
        public bool IsEnabled { get; set; }
        public bool IsTestMode { get; set; }
        public bool IsRunning { get; set; }

        public int BatchID { get; set; }
        public int ProcessID { get; set; }
        public string LastRunStatus { get; set; }
        public DateTime LastRunStartDate { get; set; }
        public DateTime LastRunEndDate { get; set; }

        public long RowsAffected { get; set; }
        public long DateDirSize { get; set; }

        public ValidationErrors ValidationErrors { get; set; }
        public string ValidationStatus { get; set; }

        public DateTime CreatedDate { get; set; }
        public string CreatedBy { get; set; }
        public string LastEditBy { get; set; }
    }
}
