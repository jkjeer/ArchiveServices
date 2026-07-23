using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class ArchieLogDetails : List<ArchieLogDetail>
    {
    }

    [DataContract(Namespace = "")]

    public class ArchieLogDetail
    {
        public ArchieLogDetail(string eventType, string taskName, long deleteRows,  bool testMode,
            string plantCode, string serverName, string databaseName, string schemaName, string tableName, Guid guid)
        {
            EventType = eventType;
            TaskName = taskName;
            DeleteRows = deleteRows;
            TestMode = testMode;
            WrittenDate = DateTime.Now;
            PlantCode = plantCode;
            ServerName = serverName;
            DatabaseName = databaseName;
            SchemaName = schemaName;
            TableName = tableName;
            Guid = guid;
        }

        public ArchieLogDetail(string eventType, string taskName, string command, long deleteRows, bool testMode,
            string plantCode, string serverName, string databaseName, string schemaName, string tableName, Guid guid)
        {
            EventType = eventType;
            TaskName = taskName;
            Command = command;
            DeleteRows = deleteRows;
            TestMode = testMode;
            WrittenDate = DateTime.Now;
            PlantCode = plantCode;
            ServerName = serverName;
            DatabaseName = databaseName;
            SchemaName = schemaName;
            TableName = tableName;
            Guid = guid;
        }

        [DataMember(Order = 10)]
        public string EventType { get; set; }
        [DataMember(Order = 50)]
        public string TaskName { get; set; }
        [DataMember(Order = 92)]
        public string Command { get; set; }

        [DataMember(Order = 130)]
        public long DeleteRows { get; set; }

        [DataMember(Order = 140)]
        public bool TestMode { get; set; }

        [DataMember(Order = 170)]
        public DateTime WrittenDate { get; set; }

        [DataMember(Order = 180)]
        public string PlantCode { get; set; }
        [DataMember(Order = 190)]
        public string TableName { get; set; }
        [DataMember(Order = 200)]
        public string SchemaName { get; set; }
        [DataMember(Order = 210)]
        public string DatabaseName { get; set; }
        [DataMember(Order = 220)]
        public string ServerName { get; set; }
        [DataMember(Order = 30)]
        public Guid Guid { get; set; }
    }
}
