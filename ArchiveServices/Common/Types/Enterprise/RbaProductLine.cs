using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace BIServices.Common.Types.Enterprise
{

    [CollectionDataContract(Namespace = "")]
    public class RbaProductLines : List<RbaProductLine>
    {
    }

    [DataContract(Namespace = "")]
    public class RbaProductLine
    {
        [DataMember(Order = 10)]
        public string ProductLineCode { get; set; }

        [DataMember(Order = 12)]
        public string ProductLineName { get; set; }

        [DataMember(Order = 20)]
        public int HourlyRunRate { get; set; }

        [DataMember(Order = 22)]
        public int ShiftHours { get; set; }

        [DataMember(Order = 24)]
        public int ShiftRunRate { get; set; }

        [DataMember(Order = 26)]
        public int RbaFloorUnits { get; set; }

        [DataMember(Order = 28)]
        public int RequiredUnits { get; set; }

        [DataMember(Order = 30)]
        public int MinFloorQty { get; set; }

        [DataMember(Order = 32)]
        public int MaxFloorQty { get; set; }

        [DataMember(Order = 34)]
        public int ReorderQty { get; set; }

        [DataMember(Order = 50)]
        public int RetireDays { get; set; }

        [DataMember(Order = 52)]
        public decimal UnitRatio { get; set; }

        [DataMember(Order = 100)]
        public int RbaFloorRacks { get; set; }

        [DataMember(Order = 110)]
        public DateTime LastShipDate { get; set; }

        [DataMember(Order = 112)]
        public DateTime LastConsumedDate { get; set; }

        [DataMember(Order = 114)]
        public int TargetShift { get; set; }

        [DataMember(Order = 116)]
        public string TargetShiftActive { get; set; }

        [DataMember(Order = 150)]
        public string Display { get; set; }

        [DataMember(Order = 200)]
        public List<RbaRack> NextRacks { get; set; }

        [DataMember(Order = 210)]
        public List<RbaRack> FloorRacks { get; set; }
    }
}
