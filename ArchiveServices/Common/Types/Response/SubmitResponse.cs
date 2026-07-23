using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Response
{

    [DataContract(Namespace = "")]
    public class SubmitResponse
    {
        [DataMember(Order = 10)]
        public string Plant { get; set; }
        [DataMember(Order = 12)]
        public string TransferType { get; set; }
        [DataMember(Order = 20)]
        public int SubscriptionId { get; set; }
        [DataMember(Order = 30)]
        public int MappingId { get; set; }
        [DataMember(Order = 32)]
        public string MappingName { get; set; }
        [DataMember(Order = 40)]
        public int DataSourceId { get; set; }
        [DataMember(Order = 42)]
        public string ConnectionString { get; set; }
        [DataMember(Order = 50)]
        public string tableName { get; set; }
        [DataMember(Order = 52)]
        public string StagingTableName { get; set; }
        [DataMember(Order = 52)]
        public string StagingViewName { get; set; }
        [DataMember(Order = 54)]
        public string SelectionCriteria { get; set; }
        [DataMember(Order = 60)]
        public bool IsActive { get; set; }
    }
}
