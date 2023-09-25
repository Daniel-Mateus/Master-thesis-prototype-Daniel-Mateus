using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ModelTransformerWebApp.Models
{
	public class FinishCreateViewModel
	{
        public int? FormModelID { get; set; }

        public BpmnExtraction? FormModel { get; set; }

        [Required]
        [DisplayName("Select the tasks to automate")]
        public Dictionary<string, string>? SelectedItems { get; set; }
	}
}

