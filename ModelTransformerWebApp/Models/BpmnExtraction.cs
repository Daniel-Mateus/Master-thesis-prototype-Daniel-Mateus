using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Web;

namespace ModelTransformerWebApp.Models
{
    public class BpmnExtraction
    {
        public int Id { get; set; }

        public DateTime ExtractionDate { get; set; }

        public List<Node>? Nodes { get; set; } = new List<Node>();

        public string? PatternsIDs { get; set; } = "";

        public List<Pattern>? Patterns { get; set; } = new List<Pattern>();

        /// <summary>
        /// EX: US1:...;US2:...;US3:...;
        /// </summary>
        public string? UserStoriesResult { get; set; } = "";

        public string? ScenarioResult { get; set; }

        [NotMapped]
        public Dictionary<string, string>? Tasks { get; set; }

        [NotMapped]
        public string[]? SelectedItems { get; set; }

        public string? FilePath { get; set; }

        [NotMapped]
        [DisplayName("Upload Diagram (.xml or .bpmn)")]
        public IFormFile? UploadFile { get; set; }
    }
}
