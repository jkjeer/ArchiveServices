using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Response;
using NLog;

namespace ArchiveServices.Common.Utilities
{
    [DataContract(Namespace = "")]
    public class BulkCopyManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static SqlBulkCopyResponse BulkTable(string tableName, DataTable dt, SqlConnection cn, SqlTransaction txn = null, int batchSize = 0)
        {
            var response = new SqlBulkCopyResponse
            {
                ConnectionString = cn.ConnectionString,
                TableName = tableName,
                StartTime = DateTime.Now
            };
            try
            {
                var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.FireTriggers, txn)
                {
                    DestinationTableName = tableName,
                    BulkCopyTimeout = ConfigurationManager.AppSettings["BulkCopyTimeout"] != null ? Convert.ToInt32(ConfigurationManager.AppSettings["BulkCopyTimeout"]) : 300,
                    BatchSize = (batchSize > 0) ? batchSize : 25000
                };
                try
                {
                    var rowsCopiedField = bulk.GetType().GetField("_rowsCopied", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    response.StartTime = DateTime.Now;
                    foreach (DataColumn col in dt.Columns)
                    {
                        var map = new SqlBulkCopyColumnMapping(col.ColumnName, col.ColumnName);
                        bulk.ColumnMappings.Add(map);
                        //Logger.Info("Column = " + col.ColumnName);
                    }
                    bulk.WriteToServer(dt);
                    //var x = rowsCopiedField.GetValue(bulk);
                    var rowsCopiedValue = (long)rowsCopiedField.GetValue(bulk);
                    response.RowsCopied = rowsCopiedValue;
                }
                catch (Exception e)
                {
                    Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    throw new Exception($"{tableName} {cn.ConnectionString} {e.Message}");
                }
                finally
                {
                    bulk.Close();
                }
            }
            catch (Exception e)
            {
                response.Errors.Add(new Error(e));
                throw new Exception($"{cn.ConnectionString} {e.Message}");
            }
            finally
            {
                response.EndTime = DateTime.Now;
            }
            return response;
        }

        public static SqlBulkCopyResponse BulkReader(string tableName, IDataReader dr, SqlConnection cn)
        {
            var response = new SqlBulkCopyResponse
            {
                ConnectionString = cn.ConnectionString,
                TableName = tableName,
                StartTime = DateTime.Now
            };
            try
            {
                var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.FireTriggers | SqlBulkCopyOptions.TableLock, null)
                {
                    DestinationTableName = tableName,
                    BulkCopyTimeout = 180, // ConfigurationManager.AppSettings["BulkCopyTimeout"] != null ? Convert.ToInt32(ConfigurationManager.AppSettings["BulkCopyTimeout"]) : 300,
                    BatchSize = 10000
                };
                var columnName = "";
                try
                {
                    var rowsCopiedField = bulk.GetType().GetField("_rowsCopied", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    response.StartTime = DateTime.Now;
                    var schemaTable = dr.GetSchemaTable();
                    if (schemaTable != null)
                        foreach (DataRow row in schemaTable.Rows)
                        {
                            columnName = row["ColumnName"].ToString();
                            var map = new SqlBulkCopyColumnMapping(columnName, columnName);
                            bulk.ColumnMappings.Add(map);
                        }
                    bulk.WriteToServer(dr);
                    var rowsCopiedValue = (long)rowsCopiedField.GetValue(bulk);
                    response.RowsCopied = rowsCopiedValue;
                }
                catch (Exception e)
                {
                    Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                    throw new ApplicationException("SourceColumn: " + columnName + ", DestinationColumn: " + columnName + ": " + e.ToString());
                }
                finally
                {
                    bulk.Close();
                }
            }
            catch (Exception e)
            {
                response.Errors.Add(new Error(e));
                response.Message = e.ToString();
                Logger.Error("ERROR: " + cn.ConnectionString + e.Message + ": " + e.StackTrace);
                throw new ApplicationException(cn.ConnectionString + "  " + e.Message + ": " + e.StackTrace);
            }
            response.EndTime = DateTime.Now;
            return response;
        }

    }
}
