using System.Collections.Generic;
using System.Runtime.Serialization;

namespace BIServices.Common.Types.Enterprise
{
    [CollectionDataContract(Namespace = "")]
    public class SqlBulkCopyResponses : List<SqlBulkCopyResponse>
    {
    }
}
