using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using BIServices.Common.Types.Response;

namespace BIServices.Common.Types.Enterprise
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

        [DataMember(Order = 10)]
        public string ConnectionString { get; set; }
        [DataMember(Order = 20)]
        public string TableName { get; set; }
        [DataMember(Order = 30)]
        public long RowsCopied { get; set; }

        [DataMember(Order = 40)]
        public DateTime StartTime { get; set; }
        [DataMember(Order = 50)]
        public DateTime EndTime { get; set; }

        [DataMember(Order = 202, EmitDefaultValue = false)]
        public Errors Errors { get; set; }
        [DataMember(Order = 60)]
        public string Message { get; set; }

        public bool HasErrors()
        {
            return Errors.Any();
        }
    }
}
