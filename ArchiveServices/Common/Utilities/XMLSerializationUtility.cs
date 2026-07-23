using System;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Newtonsoft.Json;
using Formatting = System.Xml.Formatting;

namespace ArchiveServices.Common.Utilities
{
    public class XmlSerializationUtility
    {
        public static T DeserializeObject0<T>(Encoding encoding, string xml)
        {
            try
            {
                using (var memoryStream = new MemoryStream(StringToByteArray(encoding, xml)))
                {
                    using (new XmlTextWriter(memoryStream, encoding))
                    {
                        var xmlSerializer = new XmlSerializer(typeof(T));
                        return (T)xmlSerializer.Deserialize(memoryStream);
                    }
                }
            }
            catch
            {
                return default(T);
            }
        }

        public static T DeserializeObject<T>(Encoding encoding, string objString)
        {
            Object obj;
            XmlSerializer xs = new XmlSerializer(typeof(T));
            using (MemoryStream memoryStream = new MemoryStream(StringToByteArray(encoding, objString)))
            {
                XmlTextReader xtr = new XmlTextReader(memoryStream);
                obj = xs.Deserialize(xtr);
            }
            return (T)obj;
        }

        public static string SerializeObject0<T>(Encoding encoding, T obj)
        {
            try
            {
                var memoryStream = new MemoryStream();
                using (var xmlTextWriter = new XmlTextWriter(memoryStream, encoding))
                {
                    xmlTextWriter.Formatting = Formatting.Indented;
                    var xmlSerializer = new XmlSerializer(typeof(T));
                    xmlSerializer.Serialize(xmlTextWriter, obj);
                    memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                }
                return ByteArrayToString(encoding, memoryStream.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        public static string SerializeObject<T>(Encoding encoding, T obj, bool suppressNamespace = false)
        {
            var sb = new StringBuilder();
            try
            {
                var settings = new XmlWriterSettings
                {
                    Indent = true,
                    OmitXmlDeclaration = true
                };
                //settings.ConformanceLevel = ConformanceLevel.Fragment;
                var sw = new StringWriter(sb);
                using (var writer = XmlWriter.Create(sb, settings))
                {
                    var xmlSerializer = new XmlSerializer(typeof(T));
                    if (suppressNamespace)
                    {
                        XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
                        ns.Add("", "");
                        xmlSerializer.Serialize(writer, obj, ns);
                    }
                    else
                    {
                        try
                        {
                            xmlSerializer.Serialize(writer, obj);
                        }
                        catch (Exception)
                        {
                            //var x = new Error($"{obj.GetType().FullName} {e.Message}");
                        }
                    }
                    writer.Close();
                }
                return sb.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static byte[] StringToByteArray(Encoding encoding, string xml)
        {
            return encoding.GetBytes(xml);
        }

        private static string ByteArrayToString(Encoding encoding, byte[] byteArray)
        {
            return encoding.GetString(byteArray);
        }

        public static string SerializeAnonymousType(object o, string elementName, string collectionName = null)
        {
            try
            {
                var json1 = JsonConvert.SerializeObject(o);
                var json2 = $"{{\"{elementName}\":{json1}}}";
                var doc1 = (collectionName == null)
                    ? JsonConvert.DeserializeXmlNode(json2)
                    : JsonConvert.DeserializeXmlNode(json2, collectionName);
                return doc1.DocumentElement?.OuterXml;
            }
            catch (Exception e)
            {
                throw new ApplicationException(e.Message);
            }
        }
    }
}
