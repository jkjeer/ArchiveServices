using Microsoft.Data.SqlClient;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using NLog;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.ServiceModel.Description;
using System.Text;
//using MailKit.Net.Smtp;
//using MailKit.Security;
//using MimeKit;

namespace ArchiveServices.Common.Utilities
{
    public class Mail
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public static void SendHtmlAsync(string host, string from, List<string> to, string bcc, string subject, string body)
        {
            try
            {
                var client = new SmtpClient
                {
                    Host = ConfigurationManager.AppSettings["SmtpHost"],
                    Port = int.Parse(ConfigurationManager.AppSettings["SmtpPort"]),
                    EnableSsl = bool.Parse(ConfigurationManager.AppSettings["EnableSsl"]),
                    UseDefaultCredentials = bool.Parse(ConfigurationManager.AppSettings["UseDefaultCredentials"])
                };
                //if (!bool.Parse(ConfigurationManager.AppSettings["UseDefaultCredentials"])) client.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["SendingAccount"], ConfigurationManager.AppSettings["SendingPassword"]);
                var msg = new MailMessage
                {
                    From = new MailAddress(from)
                };
                foreach (var t in to)
                {
                    msg.To.Add(t);
                }
                if (!string.IsNullOrEmpty(bcc)) msg.Bcc.Add(bcc);
                msg.Subject = subject;
                msg.IsBodyHtml = true;
                msg.Body = body;
                client.SendMailAsync(msg);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        public static void StoreResponse(string plantPathName, string subject, string xslOutput)
        {
            try
            {
                Directory.CreateDirectory(plantPathName);
                //subject = subject.Replace(".", "").Replace(":", "").Replace("[", "").Replace("]", "");
                subject = string.Join("_", subject.Split(Path.GetInvalidFileNameChars()));
                var fileName = $@"{plantPathName}\{subject} {DateTime.Now:yyyyMMdd} {DateTime.Now:hhmmss}.htm";
                File.Delete(fileName);
                File.WriteAllText(fileName, xslOutput);
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        /*****************************************************************************************************
        * Recipients
        *****************************************************************************************************/
        public static List<string> GetRecipients(SqlConnection cn, string process, string role = null, string entity = null, string eventcode = null)
        {
            var recipients = new List<string>();
            var filters = new List<string>();
            var parameters = new List<SqlParameter>();
            try
            {

                if (!string.IsNullOrEmpty(process))
                {
                    filters.Add("PROCESS_CD = @PROCESS_CD AND\r\n");
                    parameters.Add(new SqlParameter("@PROCESS_CD", SqlDbType.VarChar, 30));
                    parameters.Last().Value = process;
                }

                if (!string.IsNullOrEmpty(entity))
                {
                    filters.Add("ENTITY_CD = @ENTITY_CD AND\r\n");
                    parameters.Add(new SqlParameter("@ENTITY_CD", SqlDbType.VarChar, 12));
                    parameters.Last().Value = entity;
                }

                if (!string.IsNullOrEmpty(role))
                {
                    filters.Add("ROLE_CD = @ROLE_CD AND\r\n");
                    parameters.Add(new SqlParameter("@ROLE_CD", SqlDbType.VarChar, 12));
                    parameters.Last().Value = role;
                }

                if (!string.IsNullOrEmpty(eventcode))
                {
                    filters.Add("EVENT_CD = @EVENT_CD AND\r\n");
                    parameters.Add(new SqlParameter("@EVENT_CD", SqlDbType.VarChar, 30));
                    parameters.Last().Value = eventcode;
                }

                var cmd = GetRecipients(filters);
                foreach (var parameter in parameters)
                {
                    cmd.Parameters.Add(parameter);
                }

                cmd.Connection = cn;

                Logger.Trace(Logging.DbCmdString(cmd));
                using (IDataReader reader = cmd.ExecuteReader(CommandBehavior.SingleResult))
                {
                    while (reader.Read())
                    {
                        recipients.Add(Data.CastDbNull.To(reader["RECIPIENT_ADDRESS"], string.Empty).Trim());
                    }

                    reader.Close();
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.ToString());
            }

            return recipients;
        }

        public static SqlCommand GetRecipients(List<string> filters)
        {
            var filterString = new StringBuilder();
            foreach (var filter in filters)
            {
                filterString.Append(filter);
            }
            var cmd = new SqlCommand
            {
                CommandType = CommandType.Text,
                CommandText = $@"
                    SELECT
	                    RECIPIENT_ADDRESS
                    FROM
                        INTEGRATION.SVC.MAIL_RECIPIENT WITH (NOLOCK) 
                    WHERE
                        {filterString}
	                    IS_ACTIVE = 1"
            };
            return cmd;
        }


        //public static void SendHtmlAsync(string host, string from, string to, string bcc, string subject, string body)
        //{
        //    try
        //    {
        //        var sc = new SmtpClient
        //        {
        //            //UseDefaultCredentials = true,
        //            //Host = ConfigurationManager.AppSettings["SmtpHost"]
        //            Host = ConfigurationManager.AppSettings["SmtpHost"],
        //            UseDefaultCredentials = false,
        //            Credentials = new NetworkCredential(ConfigurationManager.AppSettings["SendingAccount"], ConfigurationManager.AppSettings["SendingPassword"]),
        //            //Port = 25,
        //            EnableSsl = true
        //        };
        //        var msg = new MailMessage
        //        {
        //            From = new MailAddress(from)
        //        };
        //        msg.To.Add(to);
        //        if (!string.IsNullOrEmpty(bcc)) msg.Bcc.Add(bcc);
        //        msg.Subject = subject;
        //        msg.IsBodyHtml = true;
        //        msg.Body = body;
        //        sc.SendMailAsync(msg);
        //    }
        //    catch (ApplicationException e)
        //    {
        //        Logger.Error($@"ERROR: {e.Message}: {e.StackTrace}");
        //        throw new ApplicationException(e.Message);
        //    }
        //}

        //public static void SendHtmlAsync(string host, string from, string to, string bcc, string subject, string body, string emailFilePath, string attachmentName)
        //public static void SendHtmlAsync(string host, string from, string to, string bcc, string subject, string body, byte[] image, string attachmentName)
        //{
        //    try
        //    {
        //        var sc = new SmtpClient
        //        {
        //            UseDefaultCredentials = true,
        //            //Host = ConfigurationManager.AppSettings["SmtpHost"]
        //            Host = ConfigurationManager.AppSettings["SmtpHost"],
        //            //UseDefaultCredentials = false,
        //            //Credentials = new NetworkCredential(ConfigurationManager.AppSettings["SendingAccount"], ConfigurationManager.AppSettings["SendingPassword"]),
        //            Port = 25,
        //            EnableSsl = true
        //        };
        //        var msg = new MailMessage
        //        {
        //            From = new MailAddress(from)
        //        };
        //        msg.To.Add(to);
        //        if (!string.IsNullOrEmpty(bcc)) msg.Bcc.Add(bcc);
        //        msg.Subject = subject;
        //        msg.IsBodyHtml = true;
        //        msg.Body = body;
        //        msg.Attachments.Add(new Attachment(new MemoryStream(image), attachmentName, "application/pdf"));
        //        sc.SendMailAsync(msg);
        //    }
        //    catch (ApplicationException e)
        //    {
        //        Logger.Error($@"ERROR: {e.Message}: {e.StackTrace}");
        //        throw new ApplicationException(e.Message);
        //    }
        //}

        //public static void SendHtmlMailKit(string host, string from, List<string> to, string bcc, string subject, string body)
        //{
        //    try
        //    {
        //        var smtpHost = Environment.GetEnvironmentVariable("SmtpHost");
        //        var smtpPort = int.Parse(Environment.GetEnvironmentVariable("SmtpPort"));
        //        var smtpUser = Environment.GetEnvironmentVariable("SendingAccount");
        //        var smtpPassword = Environment.GetEnvironmentVariable("SendingPassword");

        //        var message = new MimeMessage();
        //        message.From.Add(new MailboxAddress("svcsoa", smtpUser));
        //        message.To.Add(new MailboxAddress("Recipient", "jkjeer@cardinalcorp.com"));
        //        message.Subject = "Test Email";
        //        message.Body = new TextPart("html") { Text = body };

        //        using (var client = new SmtpClient())
        //        {
        //            client.Connect(smtpHost, smtpPort, SecureSocketOptions.StartTls);
        //            client.Authenticate(smtpUser, smtpPassword);
        //            client.Send(message);
        //            client.Disconnect(true);
        //        }

        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception(e.Message);
        //    }
        //}
    }
}
