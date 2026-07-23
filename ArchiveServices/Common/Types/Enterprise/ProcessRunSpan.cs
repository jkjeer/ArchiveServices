using System;
using System.Runtime.Serialization;
//using ArchiveServices.Common.Types.ShopFloor;

namespace ArchiveServices.Common.Types.Enterprise
{
    [DataContract(Namespace = "")]
    public class ProcessRunSpan
    {
        public ProcessRunSpan()
        {
            IsEmpty = true;
            HighWaterMark = new HighWaterMark();
        }

        [DataMember(Order = 10)]
        public DateTime StartDateTime { get; set; }

        [DataMember(Order = 20)]
        public DateTime EndDateTime { get; set; }

        [DataMember(Order = 100)]
        public HighWaterMark HighWaterMark { get; set; }

        [DataMember(Order = 200)]
        public bool IsEmpty { get; set; }

        public string Serialize()
        {
            if (HighWaterMark.Exists)
            {
                return
                    $"StartDate: {StartDateTime} EndDate: {EndDateTime} HighWaterMarkName: {HighWaterMark.HighWaterMarkName} HighWaterMarkValue: {HighWaterMark.UniqueID} MaxHighWaterMarkValue: {HighWaterMark.MaxUniqueID}";
            }
            return
                $"StartDate: {StartDateTime} EndDate: {EndDateTime}";
        }
    }
}
