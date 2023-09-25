using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ModelTransformerWebApp.Models
{
    public enum NodeType
    {
        ACTIVITY,
        STARTEVENT,
        ENDEVENT,
        START_MESSAGE_EVENT,
        END_MESSAGE_EVENT,
        INTERMEDIATE_MESSAGE_EVENT,
        EXCLUSIVE_GATEWAY,
        INTERMEDIATE_TIMER_EVENT,
        EXTERNAL_ACTOR,
    }

    public class Node
    {
        public int Id { get; set; }

        public string? BpmId { get; set; }


        /// <summary>Node Name</summary>
        public string? Name { get; set; }

        /// <summary>Order in the regular flow path.</summary>
        public int? OrderNr { get; set; } = -1;

        /// <summary>Process ID</summary>
        public string? Process { get; set; }

        /// <summary>Process Name</summary>
        public string? ProcessName { get; set; }

        /// <summary>Lane ID</summary>
        public string? Lane { get; set; }

        /// <summary>Lane Name</summary>
        public string? LaneName { get; set; }

        /// <summary>EX:Activity, Event, Gateway...</summary>
        public string? Type { get; set; }

        [ForeignKey("BpmnExtraction")]
        public int? BpmnExtractionId { get; set; }

        //Key: GatewayPath Value:isIrregularPath
        [NotMapped]
        public Dictionary<string,bool>? ExclusiveGatewayPaths { get; set; } = null;

        public Node(string? bpmId, string? name, int? orderNr, string? process, string? processName, string? lane, string? laneName, string? type)
        {
            BpmId = bpmId;
            Name = name;
            OrderNr = orderNr;
            Process = process;
            ProcessName = processName;
            Lane = lane;
            LaneName = laneName;
            Type = type;
        }



        ///// <summary>EX:Intermidiate message, Exclusive...</summary>
        //public string? SubType { get; set; }

        //public string? Name { get; set; }


    }
}
