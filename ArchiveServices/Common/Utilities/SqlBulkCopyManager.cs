using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization;
using ArchiveServices.Common.Types.Response;

namespace ArchiveServices.Common.Utilities
{
    [DataContract(Namespace = "")]
    public class SqlBulkCopyManager
    {
        public long _rowsCopied = 0;
        public string DbConnectionString { get; set; }

        public SqlBulkCopyResponse BulkTable(String TableName, DataTable dt)
        {
            var response = new SqlBulkCopyResponse();
            response.ConnectionString = DbConnectionString;
            response.TableName = TableName;

            var cn = new SqlConnection();
            try
            {
                cn = ConnectionManager.GetSqlConnection(DbConnectionString);
                cn.Open();
                var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.FireTriggers, null) {DestinationTableName = TableName};
                bulk.BulkCopyTimeout = 300;
                bulk.BatchSize = 25000;
                try
                {            
                    bulk.SqlRowsCopied += OnSqlRowsCopied;
                    bulk.NotifyAfter = 1;
                    response.StartTime = DateTime.Now;
                    foreach (DataColumn col in dt.Columns)
                    {
                        var map = new SqlBulkCopyColumnMapping(col.ColumnName, col.ColumnName);
                        bulk.ColumnMappings.Add(map);
                    }
                    bulk.WriteToServer(dt);
                }
                catch (Exception e)
                {
                    throw new ApplicationException(e.ToString());
                }
                finally
                {
                    bulk.Close();
                }

            }
            catch (Exception e)
            {
                response.Errors.Add(new Error(e.Message));
                response.Message = e.ToString();
            }
            finally
            {
                cn.Close();
            }

            response.RowsCopied = _rowsCopied;
            response.EndTime = DateTime.Now;
            return response;
        }

        public SqlBulkCopyResponse BulkReader(String TableName, IDataReader dr)
        {
            var response = new SqlBulkCopyResponse();
            response.ConnectionString = DbConnectionString;
            response.TableName = TableName;

            var cn = new SqlConnection();
            try
            {
                cn = ConnectionManager.GetSqlConnection(DbConnectionString);
                cn.Open();
                var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.FireTriggers | SqlBulkCopyOptions.TableLock, null){DestinationTableName = TableName};
                bulk.BulkCopyTimeout = 300;
                bulk.BatchSize = 25000;
                try
                {
                    bulk.SqlRowsCopied += OnSqlRowsCopied;
                    bulk.NotifyAfter = 1;
                    response.StartTime = DateTime.Now;
                    //foreach (DataRow row in dr.GetSchemaTable().Rows)
                    //{
                    //    var map = new SqlBulkCopyColumnMapping(row["ColumnName"].ToString(), row["ColumnName"].ToString());
                    //    bulk.ColumnMappings.Add(map);
                    //}
                    bulk.WriteToServer(dr);
                }
                catch (Exception e)
                {
                    throw new ApplicationException(e.ToString());
                }
                finally
                {
                    bulk.Close();
                }

            }
            catch (Exception e)
            {
                response.Errors.Add(new Error(e.Message));
                response.Message = e.ToString();
            }
            finally
            {
                cn.Close();
            }

            response.RowsCopied = _rowsCopied;
            response.EndTime = DateTime.Now;
            return response;
        }

    public SqlBulkCopyResponse BulkInsert<T>(string tableName, IList<T> list)
    {
        var response = new SqlBulkCopyResponse();
        response.ConnectionString = DbConnectionString;
        response.TableName = tableName;
        var cn = new SqlConnection();
        try
        {
            cn = ConnectionManager.GetSqlConnection(DbConnectionString);
            cn.Open();
            var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.FireTriggers | SqlBulkCopyOptions.TableLock, null);
            bulk.DestinationTableName = tableName;
            bulk.BulkCopyTimeout = 300;
            bulk.BatchSize = 25000;
            try
            {
                bulk.SqlRowsCopied += OnSqlRowsCopied;
                bulk.NotifyAfter = 1;
                response.StartTime = DateTime.Now;

                var table = new DataTable();
                var props = TypeDescriptor.GetProperties(typeof (T))
                    //Dirty hack to make sure we only have system data types                                      
                    //i.e. filter out the relationships/collections                                     
                    .Cast<PropertyDescriptor>()
                    .Where(propertyInfo => propertyInfo.PropertyType.Namespace.Equals("System"))
                    .ToArray();
                foreach (var propertyInfo in props)
                {
                    bulk.ColumnMappings.Add(propertyInfo.Name, propertyInfo.Name);
                    table.Columns.Add(propertyInfo.Name,
                                      Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType);
                }
                var values = new object[props.Length];
                foreach (var item in list)
                {
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = props[i].GetValue(item);
                    }
                    table.Rows.Add(values);
                }
                response.StartTime = DateTime.Now;
                bulk.WriteToServer(table);
            }
            catch (Exception e)
            {
                    throw new ApplicationException(e.ToString());
            }
            finally
            {
                bulk.Close();
            }
        }
        catch (Exception e)
        {
            response.Errors.Add(new Error(e.Message));
            response.Message = e.ToString();
        }
        finally
        {
            cn.Close();
        }
        response.RowsCopied = _rowsCopied;
        response.EndTime = DateTime.Now;
        return response;
    }

        private void OnSqlRowsCopied(object sender, SqlRowsCopiedEventArgs e)
        {
            _rowsCopied = e.RowsCopied;
        }
    }
}
