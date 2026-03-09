using System.Globalization;

namespace HaweeDrawingProject.Models
{
    public class Point3D
    {
        public string X { get; set; }
        public string Y { get; set; }
        public string Z { get; set; }

        public Point3D() { }
        public Point3D(double x, double y, double z)
        {
            X = x.ToString(CultureInfo.InvariantCulture);
            Y = y.ToString(CultureInfo.InvariantCulture);
            Z = z.ToString(CultureInfo.InvariantCulture);
        }
    }
}