using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Enterprise;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class ArchieLogs : List<ArchieLog>
    {
    }

    [DataContract(Namespace = "")]

    public class ArchieLog
    {
        //public ArchieLog(string eventType, string taskName, string comments, 
        //    long initRows, long deleteRows, long finalRows, bool testMode,
        //    string plantCode, string serverName, string databaseName, string schemaName, string tableName, Guid guid)
        //{
        //    var serviceContext = new ServiceContext();
        //    EventType = eventType;
        //    HostName = serviceContext.ServerAddress;
        //    ServiceName = serviceContext.HostName;
        //    Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        //    TaskName = taskName;
        //    UserName = serviceContext.UserName;
        //    Comments = comments;
        //    InitialRowCount = initRows;
        //    DeleteRows = deleteRows;
        //    FinalRowCount = finalRows;
        //    TestMode = testMode;
        //    WrittenDate = DateTime.Now;
        //    PlantCode = plantCode;
        //    ServerName = serverName;
        //    DatabaseName = databaseName;
        //    SchemaName = schemaName;
        //    TableName = tableName;
        //    Guid = guid;
        //}

        public ArchieLog(string eventType, string taskName, string comments, string command, string dateColumn, int retentionDays,
            long initRows, long deleteRows, long finalRows, bool testMode,
            string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, 
            Guid guid)
        {
            var serviceContext = new ServiceContext();
            EventType = eventType;
            Version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            TaskName = taskName;
            UserName = serviceContext.UserName;
            Comments = comments;
            Command = command;
            DateColumn = dateColumn;
            RetentionDays = retentionDays;
            InitialRowCount = initRows;
            DeleteRows = deleteRows;
            FinalRowCount = finalRows;
            TestMode = testMode;
            WrittenDate = DateTime.Now;
            GroupName = groupName;
            PlantCode = plantCode;
            ServerName = serverName;
            DatabaseName = databaseName;
            SchemaName = schemaName;
            TableName = tableName;
            //Guid = guid;
        }

        [DataMember(Order = 10)]
        public string EventType { get; set; }
        [DataMember(Order = 40)]
        public string HostName { get; set; }
        [DataMember(Order = 40)]
        public string ServiceName { get; set; }
        [DataMember(Order = 50)]
        public string TaskName { get; set; }
        [DataMember(Order = 70)]
        public string Version { get; set; }
        [DataMember(Order = 80)]
        public string UserName { get; set; }
        [DataMember(Order = 90)]
        public string Comments { get; set; }
        [DataMember(Order = 92)]
        public string Command { get; set; }
        [DataMember(Order = 100)]
        public string DateColumn { get; set; }
        [DataMember(Order = 110)]
        public int RetentionDays { get; set; }

        [DataMember(Order = 120)]
        public long InitialRowCount { get; set; }

        [DataMember(Order = 130)]
        public long DeleteRows { get; set; }

        [DataMember(Order = 140)]
        public long FinalRowCount { get; set; }

        [DataMember(Order = 140)]
        public bool TestMode { get; set; }

        [DataMember(Order = 170)]
        public DateTime WrittenDate { get; set; }

        [DataMember(Order = 180)]
        public string GroupName { get; set; }
        [DataMember(Order = 181)]
        public string PlantCode { get; set; }
        [DataMember(Order = 190)]
        public string TableName { get; set; }
        [DataMember(Order = 200)]
        public string SchemaName { get; set; }
        [DataMember(Order = 210)]
        public string DatabaseName { get; set; }
        [DataMember(Order = 220)]
        public string ServerName { get; set; }
        //[DataMember(Order = 30)]
        //public Guid Guid { get; set; }
    }
}
