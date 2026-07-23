using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Xml.Linq;
using System.Xml.Serialization;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Utilities;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class Tables : List<Table>
    {
    }

    [DataContract(Namespace = "")]

    public class Table
    {
        //public Table()
        //{
        //    StartDate = DateTime.Now;
        //    ServiceContext = new ServiceContext();
        //    AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
        //    ConnectionStrings = new ConnectionStrings();
        //    ProcessResponses = new ProcessResponses();
        //    Messages = new Messages();
        //    SerializedObjects = new SerializedObjects();
        //    Errors = new Errors();
        //    ProcessSteps = new ProcessSteps();
        //    RowsAffected = 0;
        //    TruncateStatus = "";
        //    TruncateMessage = "";
        //    TailStatus = "";
        //    TailMessage = "";
        //    ValidationErrors = new ValidationErrors();
        //    ValidationStatus = "";
        //}

        public Table(string serverName, string databaseName, string schemaName, string tableName)
        {
            StartDate = DateTime.Now;     
            ServiceContext = new ServiceContext();
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TruncateStatus = "";
            TruncateMessage = "";
            TailStatus = "";
            TailMessage = "";
            ValidationErrors = new ValidationErrors();
            ValidationStatus = "";
            TableName = tableName;
            SchemaName = schemaName;
            DatabaseName = databaseName;
            ServerName = serverName;
        }

        [DataMember(Order = 10)]
        public string TableName { get; set; }
        [DataMember(Order = 11)]
        public string SchemaName { get; set; }
        [DataMember(Order = 12)]
        public string DatabaseName { get; set; }
        [DataMember(Order = 13)]
        public string ServerName { get; set; }

        [DataMember(Order = 14)]
        public string QualifiedName => $@"[{ServerName}].[{DatabaseName}].[{SchemaName}].[{TableName}]";

        [DataMember(Order = 370)]
        public string KeyColumn { get; set; }
        [DataMember(Order = 380)]
        public string DateColumn { get; set; }
        [DataMember(Order = 390)]
        public int RetentionDays { get; set; }

        [DataMember(Order = 230)]
        public ValidationErrors ValidationErrors { get; set; }
        [DataMember(Order = 60, EmitDefaultValue = false)]
        public string ValidationStatus { get; set; }

        [DataMember(Order = 12)]
        public string TruncateStatus { get; set; }
        [DataMember(Order = 15)]
        public string TruncateMessage { get; set; }
        [DataMember(Order = 12)]
        public string TailStatus { get; set; }
        [DataMember(Order = 15)]
        public string TailMessage { get; set; }


        [DataMember(Order = 20)]
        public DateTime StartDate { get; set; }
        [DataMember(Order = 22)]
        public DateTime EndDate { get; set; }
        [DataMember(Order = 24)]
        public decimal ElapsedSeconds { get; set; }
        [DataMember(Order = 26)]
        public Errors Errors { get; set; }
        [DataMember(Order = 28)]
        public Messages Messages { get; set; }
        [DataMember(Order = 30)]
        public ConnectionStrings ConnectionStrings { get; set; }

        [DataMember(Order = 50)]
        public long RowsAffected { get; set; }
        [DataMember(Order = 52, EmitDefaultValue = false)]
        public SqlBulkCopyResponses SqlBulkCopyResponses { get; set; }
        [DataMember(Order = 54, EmitDefaultValue = false)]
        public SqlDmlResponse SqlDmlResponses { get; set; }

        [XmlIgnore]
        public SerializedObjects SerializedObjects { get; set; }

        [DataMember(Order = 100, EmitDefaultValue = false)]
        public Plant Plant { get; set; }
        [DataMember(Order = 120, EmitDefaultValue = false)]
        public string Environment { get; set; }
        [DataMember(Order = 122, EmitDefaultValue = false)]
        public string AssemblyVersion { get; set; }
        [DataMember(Order = 124, EmitDefaultValue = false)]
        public ServiceContext ServiceContext { get; set; }

        [DataMember(Order = 150)]
        public ProcessSteps ProcessSteps { get; set; }

        [DataMember(Order = 200)]
        public ProcessResponses ProcessResponses { get; set; }

        public void Close()
        {
            foreach (var childProcessResponse in ProcessResponses)
            {
                if (childProcessResponse.HasErrors())
                {
                    foreach (var error in childProcessResponse.Errors)
                    {
                        if (Errors == null) Errors = new Errors();
                        Errors.Add(error);
                    }
                }
                EndDate = DateTime.Now;
            }
        }

        public string Serialize()
        {
            try
            {
                var root = new XElement("root", XElement.Load(new StringReader(XmlSerializationUtility.SerializeObject(Encoding.UTF8, this))));
                foreach (var serializedObject in SerializedObjects)
                {
                    var element = new XElement(serializedObject.Name, XElement.Load(new StringReader(serializedObject.Value)));
                    root.Add(element);
                }
                var value = root.ToString();
                return value;
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }
    }
}
