using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;
using NLog;

namespace ArchiveServices.Common.Utilities
{
    public class XsltManager
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static string Transform(string xml, string xsltSheet, Encoding encoding)
        {
            if (Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(item => item == xsltSheet) == null)
            {
                throw new ApplicationException("Could not find embedded resource " + xsltSheet);
            }
            var response = "";
            try
            {
                var stream = new MemoryStream(encoding.GetBytes(xml));
                var document = new XPathDocument(stream);
                var strmXslt = Assembly.GetExecutingAssembly().GetManifestResourceStream(xsltSheet);
                if (strmXslt != null)
                {
                    var reader = XmlReader.Create(strmXslt);
                    var transform = new XslCompiledTransform();
                    var resolver = new EmbeddedResourceXsltResolver();
                    transform.Load(reader, XsltSettings.TrustedXslt, resolver);
                    var writer = new StringWriter();
                    transform.Transform(document, null, writer);
                    response = writer.ToString();
                }
            }
            catch (Exception e)
            {
                Logger.Error($"ERROR: {e.Message}: {e.StackTrace}");
                //stepSendMail.Errors.Add(new Error(e));
            }
            return response;
        }

        public static string Transform<T>(List<T> input, string xsltSheet, Encoding encoding)
        {

            if (Assembly.GetExecutingAssembly().GetManifestResourceNames().FirstOrDefault(item => item == xsltSheet) == null)
            {
                throw new ApplicationException("Could not find embedded resource " + xsltSheet);
            }
            var response = "";
            var xml = XmlSerializationUtility.SerializeObject(encoding, input);
            var stream = new MemoryStream(encoding.GetBytes(xml));
            var document = new XPathDocument(stream);
            var strmXslt = Assembly.GetExecutingAssembly().GetManifestResourceStream(xsltSheet);
            if (strmXslt != null)
            {
                var reader = XmlReader.Create(strmXslt);
                var transform = new XslCompiledTransform();
                var resolver = new EmbeddedResourceXsltResolver();
                transform.Load(reader, XsltSettings.TrustedXslt, resolver);
                var writer = new StringWriter();
                transform.Transform(document, null, writer);
                response = writer.ToString();
            }
            return response;
        }

        public class EmbeddedResourceXsltResolver : XmlUrlResolver
        {
            public override Uri ResolveUri(Uri baseUri, string relativeUri)
            {
                return new Uri("tdb:" + relativeUri);
            }

            public override object GetEntity(Uri absoluteUri, string role, Type ofObjectToReturn)
            {
                var executingAssembly = Assembly.GetExecutingAssembly();
                foreach (var resourceName in executingAssembly.GetManifestResourceNames())
                {
                    if (resourceName.EndsWith(("." + absoluteUri.LocalPath)))
                    {
                        var xslt = executingAssembly.GetManifestResourceStream(resourceName);
                        if (xslt != null) return XmlReader.Create(new StreamReader(xslt), null, absoluteUri.LocalPath);
                    }
                }
                throw new FileLoadException("Did not find XSLT as Embedded Resource at " + absoluteUri.LocalPath);
            }
        }
    }
}
