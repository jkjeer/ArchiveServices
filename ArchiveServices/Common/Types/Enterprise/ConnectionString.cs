using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Runtime.Serialization;
using ArchiveServices.Common.Managers.Archie;

namespace ArchiveServices.Common.Types.Enterprise
{
    [CollectionDataContract(Namespace = "")]
    public class ConnectionStrings : List<ConnectionString>
    {
    }

    [DataContract(Namespace = "")]
    public class ConnectionString
    {
        public ConnectionString()
        {
        }

        public ConnectionString(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public ConnectionString(SqlConnection cnArchConfig, string name, string value, long batchID, int processID)
        {
            Name = name;
            Value = value;
            ArchieLoggingManager.LogConnectionStringInsert(cnArchConfig, batchID, processID, name, value);
        }

        [DataMember(Order = 10)]
        public string Name { get; set; }
        [DataMember(Order = 20)]
        public string Value { get; set; }

    }
}
