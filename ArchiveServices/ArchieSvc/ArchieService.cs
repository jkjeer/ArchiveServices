using System;
using ArchiveServices.Common.Types.Archie;
using SoaCore.Wcf;

namespace ArchiveServices.ArchieSvc
{
    [ParameterInspectorLogger, CGIErrorHandler]
    public class ArchieService : IArchieService
    {
        public ProcessResponse ArchiveTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? "IG"; //string.Empty;
            plantCode = plantCode ?? "ROIG"; // string.Empty;
            serverName = serverName ?? "ROIGSQLSRV03"; // string.Empty;
            databaseName = databaseName ?? "ShopFloor"; //string.Empty;
            schemaName = schemaName ?? "EBS"; //string.Empty;
            tableName = tableName ?? "ConfiguredUnitAttribute"; //string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.ArchiveTable(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse ArchiveAndTailTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? "PY"; // string.Empty;
            plantCode = plantCode ?? "ETCT"; // string.Empty;
            serverName = serverName ?? "ETCTSQLPY"; // string.Empty;
            databaseName = databaseName ?? "ShopFloor"; // string.Empty;
            schemaName = schemaName ?? "EBS"; // string.Empty;
            tableName = tableName ?? "FuseOrder"; // string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.ArchiveAndTailTable(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse DeleteLogFiles(string groupName, string serverName, string logType, string plantCode, bool sendReport)
        {
            groupName = "CORP"; // groupName ?? string.Empty;
            serverName = "CGD2INTSVCPY"; // serverName ?? string.Empty;
            logType = "NLOG"; //logType ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ApplicationException("Group Name must have a value.");
            }
            try
            {
                return ArchieProcessor.DeleteLogFiles(groupName.ToUpper(), serverName.ToUpper(), logType.ToUpper(), plantCode.ToUpper(), sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse TailTableWithVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? "IG"; //string.Empty;
            plantCode = plantCode ?? "FRIG"; // string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? "ShopFloor"; // string.Empty;
            schemaName = schemaName ?? "dbo"; // string.Empty;
            tableName = tableName ?? "BarplaceDetail"; // string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.TailTableWithVerify(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse TailTableNoVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? "VISI"; // string.Empty;
            plantCode = plantCode ?? "ABCG"; // string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? "ShopFloor"; // string.Empty;
            schemaName = schemaName ?? "EBS"; // string.Empty;
            tableName = tableName ?? "ShipmentPlanning"; // string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.TailTableNoVerify(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public Common.Types.Archie.ProcessResponse TruncateLedgerTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? "MaintCon"; // string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName ?? string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.TruncateLedgerTable(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public Common.Types.Archie.ProcessResponse GetLedgerTableCandidates(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? "PY"; // string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? "INTEGRATION"; // string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName ?? string.Empty;
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ApplicationException("Database Name must have a value.");
            }
            try
            {
                return ArchieProcessor.GetLedgerTableCandidates(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

    }

    [ParameterInspectorLogger, CGIErrorHandler]
    public class ArchieRestService : IArchieRestService
    {
        public ProcessResponse ArchiveTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName ?? string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.ArchiveTable(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse ArchiveAndTailTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName ?? string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.ArchiveAndTailTable(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse DeleteLogFiles(string groupName, string serverName, string logType, string plantCode, bool sendReport)
        {
            groupName = groupName ?? string.Empty;
            serverName = serverName ?? string.Empty;
            logType = logType ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            if (string.IsNullOrEmpty(groupName))
            {
                throw new ApplicationException("Group Name must have a value.");
            }
            try
            {
                return ArchieProcessor.DeleteLogFiles(groupName.ToUpper(), serverName.ToUpper(), logType.ToUpper(), plantCode.ToUpper(), sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse TailTableWithVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ??  string.Empty;
            databaseName = databaseName ?? string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName ?? string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.TailTableWithVerify(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public ProcessResponse TailTableNoVerify(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName ?? string.Empty;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.TailTableNoVerify(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public Common.Types.Archie.ProcessResponse TruncateLedgerTable(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName == null ? string.Empty : tableName;
            if (!string.IsNullOrEmpty(tableName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
                if (string.IsNullOrEmpty(schemaName))
                {
                    throw new ApplicationException("Schema Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(schemaName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
                if (string.IsNullOrEmpty(databaseName))
                {
                    throw new ApplicationException("Database Name is REQUIRED.");
                }
            }
            if (!string.IsNullOrEmpty(databaseName))
            {
                if (string.IsNullOrEmpty(groupName) && string.IsNullOrEmpty(plantCode) && string.IsNullOrEmpty(serverName))
                {
                    throw new ApplicationException("Group Name, Plant Code or Server Name must have a value.");
                }
            }
            try
            {
                return ArchieProcessor.TruncateLedgerTable(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }

        public Common.Types.Archie.ProcessResponse GetLedgerTableCandidates(string groupName, string plantCode, string serverName, string databaseName, string schemaName, string tableName, bool useHighwaterMark, bool statsOnly, bool sendReport)
        {
            groupName = groupName ?? string.Empty;
            plantCode = plantCode ?? string.Empty;
            serverName = serverName ?? string.Empty;
            databaseName = databaseName ?? string.Empty;
            schemaName = schemaName ?? string.Empty;
            tableName = tableName ?? string.Empty;
            if (string.IsNullOrEmpty(databaseName))
            {
                throw new ApplicationException("Database Name must have a value.");
            }
            try
            {
                return ArchieProcessor.GetLedgerTableCandidates(groupName.ToUpper(), plantCode.ToUpper(), serverName.ToUpper(), databaseName, schemaName, tableName, useHighwaterMark, statsOnly, sendReport);
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }
    }
}
