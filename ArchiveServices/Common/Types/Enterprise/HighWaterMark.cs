using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Enterprise
{
    [CollectionDataContract(Namespace = "")]
    public class HighWaterMarks : List<HighWaterMark>
    {
    }

    [DataContract(Namespace = "")]
    public class HighWaterMark
    {

        public HighWaterMark()
        {
        }

        public HighWaterMark(string markName, string plantCode)
        {
            HighWaterMarkName = markName;
            PlantCode = plantCode;
        }

        [DataMember(Order = 2)]
        public string PlantCode { get; set; }

        //[DataMember(Order = 4)]
        //public string PlantNum { get; set; }

        [DataMember(Order = 10)]
        public int HighWaterMarkID { get; set; }

        [DataMember(Order = 12)]
        public string HighWaterMarkName { get; set; }

        [DataMember(Order = 15)]
        public DateTime StartDateTimeE1 { get; set; }

        [DataMember(Order = 16)]
        public DateTime StartDateTimeSF { get; set; }

        [DataMember(Order = 20)]
        public DateTime EnterpriseDateTime { get; set; }

        [DataMember(Order = 21)]
        public int LookbackSeconds { get; set; }

        [DataMember(Order = 22)]
        public DateTime LocalDateTime { get; set; }

        [DataMember(Order = 24)]
        public DateTime WrittenDate { get; set; }

        [DataMember(Order = 26)]
        public TimeSpan EnterpriseTimeSpan { get; set; }

        [DataMember(Order = 27)]
        public TimeSpan LocalTimeSpan { get; set; }

        [DataMember(Order = 50)]
        public long UniqueID { get; set; }

        [DataMember(Order = 52)]
        public long MaxUniqueID { get; set; }

        [DataMember(Order = 100)]
        public decimal DateComponent1 { get; set; }

        [DataMember(Order = 102)]
        public decimal TimeComponent1 { get; set; }

        [DataMember(Order = 104)]
        public decimal DateComponent2 { get; set; }

        [DataMember(Order = 106)]
        public decimal DateComponent3 { get; set; }

        [DataMember(Order = 200)]
        public bool Exists { get; set; }

        [DataMember(Order = 300)]
        public string InitStatement { get; set; }
    }
}
