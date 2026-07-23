using System;
using System.Collections.Generic;
using System.Configuration;
using Microsoft.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.Serialization;
using ArchiveServices.Common.Types.Response;
using ArchiveServices.Common.Types.Enterprise;
using ArchiveServices.Common.Utilities;

namespace ArchiveServices.Common.Types.Archie
{
    [CollectionDataContract(Namespace = "")]

    public class ProcessResponses : List<ProcessResponse>
    {
    }

    [DataContract(Namespace = "")]
    public class ProcessResponse
    {
        public ProcessResponse()
        {
            StartDate = DateTime.Now;
            ServiceContext = new ServiceContext();
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name)
        {
            StartDate = DateTime.Now;
            ServiceContext = new ServiceContext();
            ProcessName = name;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(int batchID, string batchName, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            BatchID = batchID;
            BatchName = batchName;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(int batchID, string batchName, int processID, string processName, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            BatchID = batchID;
            BatchName = batchName;
            ProcessID = processID;
            ProcessName = processName;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, ArchiveTableConfig archiveTableConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            ArchiveTableConfig = archiveTableConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, ArchiveAndTailTableConfig archiveAndTailTableConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            ArchiveAndTailTableConfig = archiveAndTailTableConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, TailTableWithVerifyConfig tailTableConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            TailTableWithVerifyConfig = tailTableConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, TailTableNoVerifyConfig tailTableNoVerifyConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            TailTableNoVerifyConfig = tailTableNoVerifyConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, TruncateLedgerTableConfig truncateLedgerTableConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            TruncateLedgerTableConfig = truncateLedgerTableConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, LedgerTableCandidateConfig ledgerTableCandidateConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            LedgerTableCandidateConfig = ledgerTableCandidateConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, DeleteLogFileConfig deleteLogFileConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            DeleteLogFileConfig = deleteLogFileConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        public ProcessResponse(string name, PlantServerConfig plantServerConfig, bool getServiceContext = false)
        {
            StartDate = DateTime.Now;
            ProcessName = name;
            ServiceContext = (getServiceContext) ? new ServiceContext() : null;
            ServerName = System.Environment.MachineName;
            AssemblyVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            Environment = ConfigurationManager.AppSettings["PrimaryEnvironment"];
            PlantServerConfig = plantServerConfig;
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponses();
            ConnectionStrings = new ConnectionStrings();
            ConnectionStrings.Add(new ConnectionString("smtp", ConfigurationManager.AppSettings["SmtpHost"]));
            ProcessResponses = new ProcessResponses();
            Messages = new Messages();
            SerializedObjects = new SerializedObjects();
            Errors = new Errors();
            ProcessSteps = new ProcessSteps();
            RowsAffected = 0;
            TransferStatus = "";
            TransferMessage = "";
            DevMode = ConfigurationManager.AppSettings["DevMode"];
            TestMode = ConfigurationManager.AppSettings["TestModeNoDelete"];
        }

        [DataMember(Order = 1)]
        public long BatchID { get; set; }
        [DataMember(Order = 2)]
        public string BatchName { get; set; }
        [DataMember(Order = 3)]
        public int ProcessID { get; set; }
        [DataMember(Order = 4)]
        public string ProcessName { get; set; }


        [DataMember(Order = 12)]
        public string TransferStatus { get; set; }

        [DataMember(Order = 15)]
        public string TransferMessage { get; set; }

        [DataMember(Order = 20)]
        public DateTime StartDate { get; set; }
        [DataMember(Order = 22)]
        public DateTime EndDate { get; set; }
        [DataMember(Order = 24)]
        public decimal ElapsedSeconds { get; set; }
        [DataMember(Order = 25)]
        public long Size { get; set; }

        [DataMember(Order = 26)]
        public Errors Errors { get; set; }
        [DataMember(Order = 28)]
        public Messages Messages { get; set; }
        [DataMember(Order = 30)]
        public ConnectionStrings ConnectionStrings { get; set; }

        //[DataMember(Order = 40)]
        //public OldArchiveTableConfig OldArchiveTableConfig { get; set; }
        [DataMember(Order = 41)]
        public TailTableNoVerifyConfig TailTableConfig { get; set; }

        public TruncateLedgerTableConfig TruncateLedgerTableConfig { get; set; }

        public LedgerTableCandidateConfig LedgerTableCandidateConfig { get; set; }


        [DataMember(Order = 42)]
        public PlantServerConfig PlantServerConfig { get; set; }

        [DataMember(Order = 43)]
        public ArchiveTableConfig ArchiveTableConfig { get; set; }

        [DataMember(Order = 43)]
        public ArchiveAndTailTableConfig ArchiveAndTailTableConfig { get; set; }

        [DataMember(Order = 43)]
        public TailTableWithVerifyConfig TailTableWithVerifyConfig { get; set; }

        [DataMember(Order = 43)]
        public TailTableNoVerifyConfig TailTableNoVerifyConfig { get; set; }

        [DataMember(Order = 44)]
        public DeleteLogFileConfig DeleteLogFileConfig { get; set; }

        [DataMember(Order = 500)]
        public long InitialRowCount { get; set; }

        [DataMember(Order = 501)]
        public long RowsToDelete { get; set; }

        [DataMember(Order = 20)]
        public DateTime SFMinDate { get; set; }

        [DataMember(Order = 20)]
        public DateTime SourceMaxDate { get; set; }

        [DataMember(Order = 20)]
        public DateTime TargetMaxDate { get; set; }
        public DateTime DestinationMaxDate { get; set; }

        [DataMember(Order = 200)]
        public DateTime ArchiveDate { get; set; }

        [DataMember(Order = 510)]
        public long RowsAffected { get; set; }

        [DataMember(Order = 520)]
        public long FinalRowCount { get; set; }

        [DataMember(Order = 520)]
        public long SourceRowCount { get; set; }

        [DataMember(Order = 520)]
        public long TargetRowCount { get; set; }
        public long DestinationRowCount { get; set; }

        [DataMember(Order = 522)]
        public long RetentionDays { get; set; }

        [DataMember(Order = 522)]
        public long SFDays { get; set; }

        [DataMember(Order = 530)]
        public string DevMode { get; set; }

        [DataMember(Order = 531)]
        public string TestMode { get; set; }

        [DataMember(Order = 540)]
        public string SelectCommand { get; set; }
        [DataMember(Order = 541)]
        public string DeleteCommand { get; set; }

        [DataMember(Order = 52, EmitDefaultValue = false)]
        public SqlBulkCopyResponses SqlBulkCopyResponses { get; set; }
        [DataMember(Order = 54, EmitDefaultValue = false)]
        public SqlDmlResponses SqlDmlResponses { get; set; }

        [DataMember(Order = 60)]
        public DateTime LookbackDate { get; set; }

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
        [DataMember(Order = 124, EmitDefaultValue = false)]
        public string ServerName { get; set; }
        [DataMember(Order = 125, EmitDefaultValue = false)]
        public string Path { get; set; }

        [DataMember(Order = 150)]
        public ProcessSteps ProcessSteps { get; set; }

        [DataMember(Order = 200)]
        public ProcessResponses ProcessResponses { get; set; }

        [DataMember(Order = 100)]
        public DateTime StartTime { get; set; }
        [DataMember(Order = 50)]
        public string Statement { get; set; }
        [DataMember(Order = 55)]
        public List<Parameter> Parameters { get; set; }
        [DataMember(Order = 300)]
        public string HandlerClass { get; set; }
        [DataMember(Order = 10)]
        public string ConnectionString { get; set; }

        public void Init(SqlCommand cmd)
        {
            StartTime = DateTime.Now.ToLocalTime();
            ConnectionString = cmd.Connection.ConnectionString;
            Statement = Regex.Replace(Regex.Replace(Regex.Replace(cmd.CommandText, " +", " "), "\r\n", ""), "\t", "").Trim();
            HandlerClass = cmd.GetType().ToString();
            Parameters = new List<Parameter>();
            foreach (SqlParameter iParameter in cmd.Parameters)
            {
                var parameter = new Parameter
                {
                    Name = iParameter.ParameterName
                };
                if (iParameter.Value != null) parameter.Value = iParameter.Value.ToString();
                Parameters.Add(parameter);
            }
        }

        public void Close()
        {
            foreach (var childProcessResponse in ProcessResponses)
            {
                if (SqlBulkCopyResponses == null) SqlBulkCopyResponses = new SqlBulkCopyResponses();
                foreach (var sqlBulkCopyResponse in childProcessResponse.SqlBulkCopyResponses)
                {
                    SqlBulkCopyResponses.Add(sqlBulkCopyResponse);
                    if (sqlBulkCopyResponse.HasErrors())
                    {
                        if (childProcessResponse.Errors == null) childProcessResponse.Errors = new Errors();
                        if (!childProcessResponse.Errors.Contains(new Error(sqlBulkCopyResponse.Message))) childProcessResponse.Errors.Add(new Error(sqlBulkCopyResponse.Message));
                    }
                }
                if (SqlDmlResponses == null) SqlDmlResponses = new SqlDmlResponses();
                foreach (var sqlDmlResponse in childProcessResponse.SqlDmlResponses)
                {
                    SqlDmlResponses.Add(sqlDmlResponse);
                }
                if (childProcessResponse.HasErrors())
                {
                    foreach (var error in childProcessResponse.Errors)
                    {
                        if (Errors == null) Errors = new Errors();
                        if (!Errors.Contains(error)) Errors.Add(error);
                    }
                }
            }
            if (!Errors.Any())
            {
                foreach (var step in ProcessSteps)
                {
                    if (step.HasErrors())
                    {
                        foreach (var error in step.Errors)
                        {
                            if (Errors == null) Errors = new Errors();
                            if (!Errors.Contains(error)) Errors.Add(error);
                        }
                    }
                    foreach (var childProcessResponse in ProcessResponses)
                    {
                        foreach (var error in childProcessResponse.Errors)
                        {
                            if (Errors == null) Errors = new Errors();
                            if (!Errors.Contains(error)) Errors.Add(error);
                        }
                    }
                }
            }
            EndDate = DateTime.Now;
            ElapsedSeconds = (decimal)(EndDate - StartDate).TotalSeconds;
            Logging.InsertProcessLog(this);
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

        public void AddStep(ProcessStep step)
        {
            ProcessSteps.Add(step);
            //if (step.Errors.Any())
            //{
            //    Errors.AddRange(step.Errors);
            //}
        }

        public void Done()
        {
            EndDate = DateTime.Now;
            ElapsedSeconds = (decimal)(EndDate - StartDate).TotalSeconds;
        }

        public bool HasErrors()
        {
            return ResponseHasErrors(this);
        }

        public bool ResponseHasErrors(ProcessResponse response)
        {
            if (response.Errors.Any() || response.ProcessSteps.Any(step => step.Errors.Any()))
            {
                return true;
            }
            return false;
        }
    }
}
