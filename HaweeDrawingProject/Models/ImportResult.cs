namespace HaweeDrawingProject.Models
{
    public class ImportResult
    {
        public int FittingsCreated { get; set; } = 0;
        public int PipesCreated { get; set; } = 0;
        public System.Collections.Generic.List<string> Messages { get; set; } = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> Errors { get; set; } = new System.Collections.Generic.List<string>();
    }
}