using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using NLog;

namespace ArchiveServices.Common.Types.Response
{
    [CollectionDataContract(Namespace = "")]
    public class ProcessLogErrors : List<ProcessLogError>
    {
    }

    [DataContract(Namespace = "")]
    public class ProcessLogError
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public ProcessLogError()
        {
        }

        [DataMember(Order = 10, EmitDefaultValue = false)]
        public DateTime EventDate { get; set; }

        [DataMember(Order = 20, EmitDefaultValue = false)]
        public string PlantCode { get; set; }

        [DataMember(Order = 30, EmitDefaultValue = false)]
        public string Process { get; set; }

        [DataMember(Order = 40, EmitDefaultValue = false)]
        public string Status { get; set; }

        [DataMember(Order = 50, EmitDefaultValue = false)]
        public DateTime StartDate { get; set; }

        [DataMember(Order = 60, EmitDefaultValue = false)]
        public DateTime EndDate { get; set; }

        [DataMember(Order = 70, EmitDefaultValue = false)]
        public string Message { get; set; }

        [DataMember(Order = 80, EmitDefaultValue = false)]
        public string Source { get; set; }

        [DataMember(Order = 90, EmitDefaultValue = false)]
        public string StackTrace { get; set; }

    }
}
