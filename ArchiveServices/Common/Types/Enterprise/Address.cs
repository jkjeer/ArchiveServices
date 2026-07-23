using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Enterprise
{

    [DataContract(Namespace = "")]
    public class Address
    {
        [DataMember(Order = 10)]
        public decimal AddressNum { get; set; }

        [DataMember(Order = 12)]
        public string Name { get; set; }

        [DataMember(Order = 14)]
        public string Address1 { get; set; }

        [DataMember(Order = 16)]
        public string Address2 { get; set; }

        [DataMember(Order = 18)]
        public string Address3 { get; set; }

        [DataMember(Order = 20)]
        public string Address4 { get; set; }

        [DataMember(Order = 22)]
        public string Address5 { get; set; }

        [DataMember(Order = 24)]
        public string City { get; set; }

        [DataMember(Order = 26)]
        public string State { get; set; }

        [DataMember(Order = 28)]
        public string PostalCode { get; set; }

        [DataMember(Order = 30)]
        public string Country { get; set; }
    }

}
