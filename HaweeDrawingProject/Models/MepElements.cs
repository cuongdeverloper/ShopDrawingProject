using System.Collections.Generic;

namespace HaweeDrawingProject.Models
{
    public class PipeData
    {
        public string Id { get; set; }
        public int LevelId { get; set; } 
        public string LevelName { get; set; }
        public string SystemTypeName { get; set; }
        public string PipeTypeName { get; set; }
        public double Diameter { get; set; }
        public Point3D StartPoint { get; set; }
        public Point3D EndPoint { get; set; }
        public List<ConnectorInfo> Connectors { get; set; }
    }

    public class FittingData
    {
        public TransformData Transform { get; set; }
        public string Id { get; set; }
        public int LevelId { get; set; } 
        public string LevelName { get; set; }
        public string FamilyName { get; set; }
        public string TypeName { get; set; }
        public Point3D LocationPoint { get; set; }
        public double Angle { get; set; }
        public List<ConnectorInfo> Connectors { get; set; }
    }

    public class ExportDocument
    {
        public List<PipeData> Pipes { get; set; } = new List<PipeData>();
        public List<FittingData> Fittings { get; set; } = new List<FittingData>();
    }
}