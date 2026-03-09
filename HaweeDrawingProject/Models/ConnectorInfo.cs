namespace HaweeDrawingProject.Models
{
    public class ConnectorInfo
    {
        public int ConnectorId { get; set; }
        public Point3D Origin { get; set; }
        public double Diameter { get; set; }
        public string ConnectedToId { get; set; }
    }
}