using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Response
{
    [CollectionDataContract(Namespace = "")]
    public class SqlBulkCopyResponses : List<SqlBulkCopyResponse>
    {
    }

    [DataContract(Namespace = "")]
    public class SqlBulkCopyResponse
    {
        public SqlBulkCopyResponse()
        {
            StartTime = DateTime.Now;
            Errors = new Errors();
        }

        [DataMember(Order = 10, EmitDefaultValue = false)]
        public string ConnectionString { get; set; }
        [DataMember(Order = 20, EmitDefaultValue = false)]
        public string TableName { get; set; }
        [DataMember(Order = 30, EmitDefaultValue = false)]
        public long RowsCopied { get; set; }

        [DataMember(Order = 100, EmitDefaultValue = false)]
        public DateTime StartTime { get; set; }
        [DataMember(Order = 110, EmitDefaultValue = false)]
        public DateTime EndTime { get; set; }
        [DataMember(Order = 112, EmitDefaultValue = false)]
        public decimal ElapsedSeconds { get; set; }

        [DataMember(Order = 202, EmitDefaultValue = false)]
        public Errors Errors { get; set; }
        [DataMember(Order = 210, EmitDefaultValue = false)]
        public string Message { get; set; }

        public bool HasErrors()
        {
            return Errors.Any();
        }
    }
}
