using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class KeyColumns : List<KeyColumn>
    {
    }

    [DataContract(Namespace = "")]

    public class KeyColumn
    {
        public KeyColumn()
        {
            ColumnName = "";
            DataType = "";
        }

        public KeyColumn(string columnName)
        {
            ColumnName = columnName;
            //DataType = dataType;
        }

        public KeyColumn(string columnName, string dataType)
        {
            ColumnName = columnName;
            DataType = dataType;
        }

        [DataMember(Order = 10)]
        public string FieldDef { get; set; }
        [DataMember(Order = 10)]
        public string AlterDataType { get; set; }

        [DataMember(Order = 10)]
        public string ColumnName { get; set; }

        [DataMember(Order = 11)]
        public string DataType { get; set; }

        [DataMember(Order = 12)]
        public Int16 MaxLength { get; set; }

        [DataMember(Order = 13)]
        public Byte Precision { get; set; }

        [DataMember(Order = 14)]
        public Byte Scale { get; set; }

        [DataMember(Order = 14)]
        public bool IsNullable { get; set; }

        //[DataMember(Order = 14)]
        //public string QualifiedName => $@"[{ServerName}].[{DatabaseName}].[{SchemaName}].[{TableName}]";

        //public void Close()
        //{
        //    foreach (var childProcessResponse in ProcessResponses)
        //    {
        //        if (childProcessResponse.HasErrors())
        //        {
        //            foreach (var error in childProcessResponse.Errors)
        //            {
        //                if (Errors == null) Errors = new Errors();
        //                Errors.Add(error);
        //            }
        //        }
        //        EndDate = DateTime.Now;
        //    }
        //}

    }
}
