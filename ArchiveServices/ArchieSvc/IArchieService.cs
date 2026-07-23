using System.ServiceModel;
using System.ServiceModel.Web;
using ArchiveServices.Common.Types.Archie;
using SoaCore.Wcf;

namespace ArchiveServices.ArchieSvc
{
    [ServiceContract]
    public interface IArchieService
    {
        [OperationContract, OperationInvokerLogger]
        ProcessResponse ArchiveTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        ProcessResponse ArchiveAndTailTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        ProcessResponse DeleteLogFiles(string groupName, string serverName, string logType, string plantCode, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        ProcessResponse TailTableWithVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        ProcessResponse TailTableNoVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        ProcessResponse TruncateLedgerTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        ProcessResponse GetLedgerTableCandidates(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);
    }

    [ServiceContract]
    public interface IArchieRestService
    {
        [OperationContract, OperationInvokerLogger]
        [WebGet(UriTemplate = "/ArchiveTable?GroupName={groupName}&PlantCode={plantCode}&ServerName={serverName}&DatabaseName={databaseName}&SchemaName={schemaName}&TableName={tableName}&UseHighwaterMark={useHighwaterMark}&StatsOnly={statsOnly}&SendReport={sendReport}")]
        ProcessResponse ArchiveTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        [WebGet(UriTemplate = "/ArchiveAndTailTable?GroupName={groupName}&PlantCode={plantCode}&ServerName={serverName}&DatabaseName={databaseName}&SchemaName={schemaName}&TableName={tableName}&UseHighwaterMark={useHighwaterMark}&StatsOnly={statsOnly}&SendReport={sendReport}")]
        ProcessResponse ArchiveAndTailTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        [WebGet(UriTemplate = "/DeleteLogFiles?GroupName={groupName}&ServerName={serverName}&LogType={logType}&PlantCode={plantCode}&SendReport={sendReport}")]
        ProcessResponse DeleteLogFiles(string groupName, string serverName, string logType, string plantCode, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        [WebGet(UriTemplate = "/TailTableWithVerify?GroupName={groupName}&PlantCode={plantCode}&ServerName={serverName}&DatabaseName={databaseName}&SchemaName={schemaName}&TableName={tableName}&UseHighwaterMark={useHighwaterMark}&StatsOnly={statsOnly}&SendReport={sendReport}")]
        ProcessResponse TailTableWithVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        [WebGet(UriTemplate = "/TailTableNoVerify?GroupName={groupName}&PlantCode={plantCode}&ServerName={serverName}&DatabaseName={databaseName}&SchemaName={schemaName}&TableName={tableName}&UseHighwaterMark={useHighwaterMark}&StatsOnly={statsOnly}&SendReport={sendReport}")]
        ProcessResponse TailTableNoVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        [WebGet(UriTemplate = "/TruncateLedgerTable?GroupName={groupName}&PlantCode={plantCode}&ServerName={serverName}&DatabaseName={databaseName}&SchemaName={schemaName}&TableName={tableName}&UseHighwaterMark={useHighwaterMark}&StatsOnly={statsOnly}&SendReport={sendReport}")]
        ProcessResponse TruncateLedgerTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);

        [OperationContract, OperationInvokerLogger]
        [WebGet(UriTemplate = "/GetLedgerTableCandidates?GroupName={groupName}&PlantCode={plantCode}&ServerName={serverName}&DatabaseName={databaseName}&SchemaName={schemaName}&TableName={tableName}&UseHighwaterMark={useHighwaterMark}&StatsOnly={statsOnly}&SendReport={sendReport}")]
        ProcessResponse GetLedgerTableCandidates(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport);
    }
}