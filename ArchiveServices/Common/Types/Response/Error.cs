using System;
using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Serialization;
using System.ServiceModel;
using NLog;

namespace ArchiveServices.Common.Types.Response
{
    [CollectionDataContract(Namespace = "")]
    public class Errors : List<Error>
    {
    }

    [DataContract(Namespace = "")]
    public class Error
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

        public Error()
        {
        }

        public Error(string message, bool logError = true)
        {
            Message = message;
            Source = "";
            StackTrace = "";
            if (logError)
            {
                if (OperationContext.Current == null)
                {
                    OperationContext.Current = (OperationContext)CallContext.LogicalGetData("WcfOperationContext");
                }
                OperationContext operationContext = OperationContext.Current;
                LogEventInfo logEvent = new LogEventInfo
                {
                    Level = LogLevel.Error,
                    LoggerName = GetType().FullName
                };
                logEvent.Properties.Add("EVENT_DATE", DateTime.Now);
                logEvent.Properties.Add("EVENT_TYPE", "ERROR");
                logEvent.Properties.Add("OPERATION_NAME", "");
                logEvent.Properties.Add("LOG_INTERFACE", "");
                logEvent.Properties.Add("ARGUMENTS", "");
                try
                {
                    logEvent.Properties.Add("USER_NAME", ServiceSecurityContext.Current?.WindowsIdentity.Name);
                }
                catch
                {
                    logEvent.Properties.Add("USER_NAME", "");
                }
                try
                {
                    logEvent.Properties.Add("CONTRACT_NAME", operationContext?.EndpointDispatcher?.ContractName);
                }
                catch
                {
                    logEvent.Properties.Add("CONTRACT_NAME", "");
                }
                try
                {
                    logEvent.Properties.Add("HOST_NAME", operationContext?.InstanceContext?.Host?.Description?.Name);
                }
                catch
                {
                    logEvent.Properties.Add("HOST_NAME", "");
                }
                logEvent.Properties.Add("SCHEME", operationContext?.EndpointDispatcher?.EndpointAddress?.Uri.Scheme);
                logEvent.Properties.Add("PORT", operationContext?.EndpointDispatcher?.EndpointAddress?.Uri.Port);
                logEvent.Properties.Add("SERVER_ADDRESS", operationContext?.Channel.LocalAddress.Uri.ToString());
                logEvent.Properties.Add("SESSION_ID", operationContext?.SessionId);
                logEvent.Properties.Add("ELAPSED_MS", 0);
                logEvent.Properties.Add("COMMENTS", message);
                Logger.Log(logEvent);
            }
        }

        public Error(Exception exception, bool logError = true)
        {
            Message = exception.Message;
            Source = exception.Source;
            StackTrace = exception.StackTrace;
            if (logError)
            {
                if (OperationContext.Current == null)
                {
                    OperationContext.Current = (OperationContext)CallContext.LogicalGetData("WcfOperationContext");
                }
                OperationContext operationContext = OperationContext.Current;
                LogEventInfo logEvent = new LogEventInfo
                {
                    Level = LogLevel.Error,
                    LoggerName = GetType().FullName
                };
                logEvent.Properties.Add("EVENT_DATE", DateTime.Now);
                logEvent.Properties.Add("EVENT_TYPE", "ERROR");
                logEvent.Properties.Add("OPERATION_NAME", "");
                logEvent.Properties.Add("LOG_INTERFACE", "");
                logEvent.Properties.Add("ARGUMENTS", "");
                try
                {
                    logEvent.Properties.Add("USER_NAME", ServiceSecurityContext.Current?.WindowsIdentity.Name);
                }
                catch
                {
                    logEvent.Properties.Add("USER_NAME", "");
                }
                try
                {
                    logEvent.Properties.Add("CONTRACT_NAME", operationContext?.EndpointDispatcher?.ContractName);
                }
                catch
                {
                    logEvent.Properties.Add("CONTRACT_NAME", "");
                }
                try
                {
                    logEvent.Properties.Add("HOST_NAME", operationContext?.InstanceContext?.Host?.Description?.Name);
                }
                catch
                {
                    logEvent.Properties.Add("HOST_NAME", "");
                }
                logEvent.Properties.Add("SCHEME", operationContext?.EndpointDispatcher?.EndpointAddress?.Uri.Scheme);
                logEvent.Properties.Add("PORT", operationContext?.EndpointDispatcher?.EndpointAddress?.Uri.Port);
                logEvent.Properties.Add("SERVER_ADDRESS", operationContext?.Channel.LocalAddress.Uri.ToString());
                logEvent.Properties.Add("SESSION_ID", operationContext?.SessionId);
                logEvent.Properties.Add("ELAPSED_MS", 0);
                logEvent.Properties.Add("COMMENTS", exception.Message);
                Logger.Log(logEvent);
            }
        }

        [DataMember(Order = 20, EmitDefaultValue = false)]
        public string Message { get; set; }

        [DataMember(Order = 30, EmitDefaultValue = false)]
        public string Source { get; set; }

        [DataMember(Order = 40, EmitDefaultValue = false)]
        public string StackTrace { get; set; }
    }
}
