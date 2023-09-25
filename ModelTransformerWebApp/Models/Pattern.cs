namespace ModelTransformerWebApp.Models
{
    public class Pattern
    {
        public int Id { get; set; }

        /// <summary>Node types ordered and separated by commas. EX: ACTIVITY;EXCLUSIVE_GATEWAY;ACTIVITY</summary>
        public string? NodeTypes { get; set; }

        public bool? IsSameLane { get; set; }

        public bool? IsSameProcess { get; set; }

        /// <summary> As {LANE} I want to {ACTIVITY} in order to {ACTIVITY};</summary>
        public string? USTemplate { get; set; }

        public bool? IsUSPattern { get; set; }


        public Pattern(string? nodeTypes, bool? isSameLane, bool? isSameProcess, string? uSTemplate, bool? isUSPattern = true)
        {
            NodeTypes = nodeTypes;
            IsSameLane = isSameLane;
            IsSameProcess = isSameProcess;
            USTemplate = uSTemplate;
            IsUSPattern = isUSPattern;
        }
    }
}
