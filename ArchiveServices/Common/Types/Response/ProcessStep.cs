using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Runtime.Serialization;
using ArchiveServices.Common.Managers.Archie;
using ArchiveServices.Common.Types.Archie;
using ArchiveServices.Common.Types.Response;

namespace ArchiveServices.Common.Types.Enterprise
{
    [CollectionDataContract(Namespace = "")]
    public class ProcessSteps : List<ProcessStep>
    {
    }

    [DataContract(Namespace = "")]
    public class ProcessStep
    {
        public ProcessStep()
        {
        }

        //public ProcessStep(int processID)
        //{
        //    ProcessID = processID;
        //    StartDate = DateTime.Now;
        //    Errors = new Errors();
        //    Messages = new Messages();
        //    SqlBulkCopyResponses = new SqlBulkCopyResponses();
        //    SqlDmlResponses = new SqlDmlResponses();
        //    RowsAffected = 0;
        //}

        public ProcessStep(string stepName)
        {
            StepName = stepName;
            StartDate = DateTime.Now;
            Errors = new Errors();
            Messages = new Messages();
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponse();
            ProcessResponses = new ProcessResponses();
            RowsAffected = 0;
            Statement = "";
        }

        public ProcessStep(SqlConnection cnArchConfig, int processID, string stepName)
        {
            ProcessID = processID;
            StepName = stepName;
            StartDate = DateTime.Now;
            Errors = new Errors();
            Messages = new Messages();
            SqlBulkCopyResponses = new SqlBulkCopyResponses();
            SqlDmlResponses = new SqlDmlResponse();
            ProcessResponses = new ProcessResponses();
            RowsAffected = 0;
            Statement = "";
            StepID = ArchieLoggingManager.LogStepInsert(cnArchConfig, stepName, processID);
        }

        [DataMember(Order = 10)]
        public int StepID { get; set; }
        [DataMember(Order = 11)]
        public string StepName { get; set; }
        [DataMember(Order = 20)]
        public int ProcessID { get; set; }

        [DataMember(Order = 30, EmitDefaultValue = false)]
        public DateTime StartDate { get; set; }
        [DataMember(Order = 40, EmitDefaultValue = false)]
        public DateTime EndDate { get; set; }
        [DataMember(Order = 50, EmitDefaultValue = false)]
        public decimal ElapsedSeconds { get; set; }
        [DataMember(Order = 60, EmitDefaultValue = true)]
        public long RowsAffected { get; set; }
        [DataMember(Order = 70, EmitDefaultValue = true)]
        public string Statement { get; set; }

        [DataMember(Order = 40, EmitDefaultValue = true)]
        public long Size { get; set; }
        [DataMember(Order = 100, EmitDefaultValue = false)]
        public Messages Messages { get; set; }
        [DataMember(Order = 200, EmitDefaultValue = false)]
        public Errors Errors { get; set; }
        [DataMember(Order = 110, EmitDefaultValue = false)]
        public SqlBulkCopyResponses SqlBulkCopyResponses { get; set; }
        [DataMember(Order = 120, EmitDefaultValue = false)]
        public SqlDmlResponse SqlDmlResponses { get; set; }
        [DataMember(Order = 200, EmitDefaultValue = false)]
        public ProcessResponses ProcessResponses { get; set; }

        public void Done()
        {
            EndDate = DateTime.Now;
            ElapsedSeconds = (decimal)(EndDate - StartDate).TotalSeconds;
        }

        public void Done(SqlConnection cnArchConfig)
        {
            this.Statement = this.Statement.Trim().Replace("  ", " ");
            EndDate = DateTime.Now;
            ElapsedSeconds = (decimal)(EndDate - StartDate).TotalSeconds;
            ArchieLoggingManager.LogStepUpdateDone(cnArchConfig, this);
        }

        public bool HasErrors()
        {
            return ProcessStepHasErrors(this);
            //return Errors.Any();
        }

        public bool ProcessStepHasErrors(ProcessStep processStep)
        {
            if (Errors.Any())
            {
                return true;
            }
            foreach (var childResponse in processStep.ProcessResponses)
            {
                if (childResponse.Errors.Any() || childResponse.ProcessSteps.Any(step => step.Errors.Any()))
                {
                    return true;
                }
                if (ResponseHasErrors(childResponse))
                {
                    return true;
                }
            }
            return false;
        }

        public bool ResponseHasErrors(ProcessResponse response)
        {
            if (response.Errors.Any())
            {
                return true;
            }
            foreach (var childResponse in response.ProcessResponses)
            {
                if (childResponse.Errors.Any() || childResponse.ProcessSteps.Any(step => step.Errors.Any()))
                {
                    return true;
                }
                if (ResponseHasErrors(childResponse))
                {
                    return true;
                }
            }
            foreach (var step in response.ProcessSteps)
            {
                if (step.Errors.Any())
                {
                    return true;
                }
                if (step.ProcessResponses.Any(ResponseHasErrors))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
