using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Enterprise
{

    [CollectionDataContract(Namespace = "")]
    public class PingResults : List<PingResult>
    {
    }

    public class PingResult
    {
    }
}
