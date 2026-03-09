using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Plumbing;
using HaweeDrawingProject.Models;
using HaweeDrawingProject.ViewModels;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace HaweeDrawingProject.Services
{
    public class WarningSwallower : IFailuresPreprocessor
    {
        public FailureProcessingResult PreprocessFailures(FailuresAccessor a)
        {
            IList<FailureMessageAccessor> failures = a.GetFailureMessages();
            if (failures.Count == 0) return FailureProcessingResult.Continue;

            foreach (FailureMessageAccessor f in failures)
            {
                if (f.GetSeverity() == FailureSeverity.Warning)
                {
                    a.DeleteWarning(f);
                }
                else
                {
                    a.ResolveFailure(f);
                }
            }
            return FailureProcessingResult.ProceedWithCommit;
        }
    }

    public class RevitPipeService
    {
        private Document _doc;

        public RevitPipeService(Document doc)
        {
            _doc = doc;
        }

        public List<LevelItem> GetLevelItems()
        {
            var list = new List<LevelItem>();
            list.Add(new LevelItem { Name = "< Full - Tất cả hệ thống >", LevelId = ElementId.InvalidElementId });

            var levels = new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation);

            foreach (var lvl in levels)
            {
                list.Add(new LevelItem { Name = lvl.Name, LevelId = lvl.Id });
            }
            return list;
        }

        private List<ConnectorInfo> GetConnectorsData(ConnectorManager cm)
        {
            var list = new List<ConnectorInfo>();
            if (cm == null) return list;

            foreach (Connector conn in cm.Connectors)
            {
                string connectedTo = "";
                if (conn.IsConnected)
                {
                    foreach (Connector refConn in conn.AllRefs)
                    {
                        if (refConn.Owner.Id != conn.Owner.Id && refConn.ConnectorType != ConnectorType.Logical)
                        {
                            connectedTo = refConn.Owner.Id.ToString();
                            break;
                        }
                    }
                }

                list.Add(new ConnectorInfo
                {
                    ConnectorId = conn.Id,
                    Origin = new Point3D(conn.Origin.X, conn.Origin.Y, conn.Origin.Z),
                    Diameter = conn.Radius * 2,
                    ConnectedToId = connectedTo
                });
            }
            return list;
        }

        public void ExportPipesToJson(ElementId targetLevelId, string filePath)
        {
            ExportDocument exportDoc = new ExportDocument();

            var pipeCollector = new FilteredElementCollector(_doc).OfClass(typeof(Pipe));
            var fittingCollector = new FilteredElementCollector(_doc).OfCategory(BuiltInCategory.OST_PipeFitting).WhereElementIsNotElementType();

            if (targetLevelId != ElementId.InvalidElementId)
            {
                pipeCollector.WherePasses(new ElementLevelFilter(targetLevelId));
                fittingCollector.WherePasses(new ElementLevelFilter(targetLevelId));
            }

            foreach (Pipe pipe in pipeCollector.Cast<Pipe>())
            {
                Curve curve = (pipe.Location as LocationCurve).Curve;
                string sysName = pipe.get_Parameter(BuiltInParameter.RBS_PIPING_SYSTEM_TYPE_PARAM)?.AsValueString() ?? "";

                int levelIdInt = pipe.ReferenceLevel?.Id.IntegerValue ?? pipe.LevelId.IntegerValue;
                string levelName = _doc.GetElement(new ElementId((long)levelIdInt))?.Name ?? "";

                exportDoc.Pipes.Add(new PipeData
                {
                    Id = pipe.Id.ToString(),
                    LevelId = levelIdInt,
                    LevelName = levelName,
                    SystemTypeName = sysName,
                    PipeTypeName = pipe.PipeType?.Name ?? "",
                    Diameter = pipe.Diameter,
                    StartPoint = new Point3D(curve.GetEndPoint(0).X, curve.GetEndPoint(0).Y, curve.GetEndPoint(0).Z),
                    EndPoint = new Point3D(curve.GetEndPoint(1).X, curve.GetEndPoint(1).Y, curve.GetEndPoint(1).Z),
                    Connectors = GetConnectorsData(pipe.ConnectorManager)
                });
            }

            foreach (FamilyInstance fitting in fittingCollector.Cast<FamilyInstance>())
            {
                LocationPoint locPoint = fitting.Location as LocationPoint;
                if (locPoint == null) continue;

                Transform tf = fitting.GetTransform();
                int levelIdInt = fitting.LevelId.IntegerValue;
                string levelName = _doc.GetElement(fitting.LevelId)?.Name ?? "";

                double angleDegrees = locPoint.Rotation * (180.0 / Math.PI);

                exportDoc.Fittings.Add(new FittingData
                {
                    Id = fitting.Id.ToString(),
                    LevelId = levelIdInt,
                    LevelName = levelName,
                    FamilyName = fitting.Symbol.FamilyName,
                    TypeName = fitting.Name,
                    LocationPoint = new Point3D(locPoint.Point.X, locPoint.Point.Y, locPoint.Point.Z),
                    Angle = angleDegrees,
                    Transform = new TransformData
                    {
                        Origin = new Point3D(tf.Origin.X, tf.Origin.Y, tf.Origin.Z),
                        BasisX = new Point3D(tf.BasisX.X, tf.BasisX.Y, tf.BasisX.Z),
                        BasisY = new Point3D(tf.BasisY.X, tf.BasisY.Y, tf.BasisY.Z),
                        BasisZ = new Point3D(tf.BasisZ.X, tf.BasisZ.Y, tf.BasisZ.Z)
                    },
                    Connectors = GetConnectorsData(fitting.MEPModel?.ConnectorManager)
                });
            }

            string json = JsonConvert.SerializeObject(exportDoc, Formatting.Indented);
            File.WriteAllText(filePath, json);
        }

        private XYZ ParseXYZ(Point3D p)
        {
            double x = double.Parse(p.X, System.Globalization.CultureInfo.InvariantCulture);
            double y = double.Parse(p.Y, System.Globalization.CultureInfo.InvariantCulture);
            double z = double.Parse(p.Z, System.Globalization.CultureInfo.InvariantCulture);
            return new XYZ(x, y, z);
        }

        private Connector GetClosestConnector(Element e, XYZ point)
        {
            ConnectorManager cm = null;
            if (e is Pipe p) cm = p.ConnectorManager;
            else if (e is FamilyInstance fi) cm = fi.MEPModel?.ConnectorManager;

            if (cm == null) return null;

            Connector closest = null;
            double minDist = double.MaxValue;
            foreach (Connector c in cm.Connectors)
            {
                double dist = c.Origin.DistanceTo(point);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = c;
                }
            }
            return closest;
        }

        private void SetFittingSize(FamilyInstance fitting, FittingData data)
        {
            if (data.Connectors == null || data.Connectors.Count == 0) return;

            double targetDiameter = data.Connectors[0].Diameter;
            double targetRadius = targetDiameter / 2.0;

            string[] possibleParamNames = { "Nominal Diameter 1", "Nominal Diameter 2", "Nominal Radius 1", "Size", "D1", "D2", "d" };

            foreach (string paramName in possibleParamNames)
            {
                Parameter p = fitting.LookupParameter(paramName);
                if (p != null && !p.IsReadOnly)
                {
                    try
                    {
                        if (paramName.Contains("Radius"))
                        {
                            p.Set(targetRadius);
                        }
                        else
                        {
                            p.Set(targetDiameter);
                        }
                    }
                    catch { }
                }
            }
        }

        public void ImportPipesFromJson(string filePath, ElementId targetLevelId)
        {
            string json = File.ReadAllText(filePath);
            ExportDocument exportDoc = JsonConvert.DeserializeObject<ExportDocument>(json);

            var allPipeTypes = new FilteredElementCollector(_doc).OfClass(typeof(PipeType)).Cast<PipeType>().ToList();
            var allSystemTypes = new FilteredElementCollector(_doc).OfClass(typeof(PipingSystemType)).Cast<PipingSystemType>().ToList();
            var allSymbols = new FilteredElementCollector(_doc).OfClass(typeof(FamilySymbol)).OfCategory(BuiltInCategory.OST_PipeFitting).Cast<FamilySymbol>().ToList();
            var allLevels = new FilteredElementCollector(_doc).OfClass(typeof(Level)).Cast<Level>().ToList();
            ElementId fallbackLevelId = allLevels.FirstOrDefault()?.Id;

            if (allSymbols.Count == 0 && exportDoc.Fittings.Count > 0)
            {
                System.Windows.MessageBox.Show("CẢNH BÁO: Project này chưa được load bất kỳ Family Phụ kiện nào.\nHệ thống sẽ chỉ vẽ được đường ống thẳng!", "Cảnh báo", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }

            Dictionary<string, Element> idMap = new Dictionary<string, Element>();

            using (Transaction t = new Transaction(_doc, "Import MEP Elements"))
            {
                FailureHandlingOptions options = t.GetFailureHandlingOptions();
                options.SetFailuresPreprocessor(new WarningSwallower());
                t.SetFailureHandlingOptions(options);

                t.Start();

                foreach (var fittingData in exportDoc.Fittings)
                {
                    XYZ location = ParseXYZ(fittingData.LocationPoint);
                    ElementId fittingLevelId = targetLevelId != ElementId.InvalidElementId ? targetLevelId : (allLevels.FirstOrDefault(l => l.Name == fittingData.LevelName)?.Id ?? fallbackLevelId);

                    FamilySymbol symbol = allSymbols.FirstOrDefault(x => x.FamilyName == fittingData.FamilyName && x.Name == fittingData.TypeName);
                    if (symbol == null && allSymbols.Count > 0) symbol = allSymbols.FirstOrDefault();

                    if (symbol != null)
                    {
                        if (!symbol.IsActive) symbol.Activate();
                        FamilyInstance newFitting = _doc.Create.NewFamilyInstance(location, symbol, _doc.GetElement(fittingLevelId) as Level, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                        SetFittingSize(newFitting, fittingData);

                        if (fittingData.Connectors != null && fittingData.Connectors.Count == 2)
                        {
                            string pipe1Id = fittingData.Connectors[0].ConnectedToId;
                            string pipe2Id = fittingData.Connectors[1].ConnectedToId;

                            var pipe1Data = exportDoc.Pipes.FirstOrDefault(p => p.Id == pipe1Id);
                            var pipe2Data = exportDoc.Pipes.FirstOrDefault(p => p.Id == pipe2Id);

                            if (pipe1Data != null && pipe2Data != null)
                            {
                                XYZ center = location;

                                XYZ p1Start = ParseXYZ(pipe1Data.StartPoint);
                                XYZ p1End = ParseXYZ(pipe1Data.EndPoint);
                                XYZ p1FarPoint = (p1Start.DistanceTo(center) > p1End.DistanceTo(center)) ? p1Start : p1End;

                                XYZ p2Start = ParseXYZ(pipe2Data.StartPoint);
                                XYZ p2End = ParseXYZ(pipe2Data.EndPoint);
                                XYZ p2FarPoint = (p2Start.DistanceTo(center) > p2End.DistanceTo(center)) ? p2Start : p2End;

                                XYZ v1 = new XYZ(p1FarPoint.X - center.X, p1FarPoint.Y - center.Y, 0).Normalize();
                                XYZ v2 = new XYZ(p2FarPoint.X - center.X, p2FarPoint.Y - center.Y, 0).Normalize();

                                XYZ vSum = v1 + v2;
                                double alphaRadian = Math.Atan2(vSum.Y, vSum.X);
                                double alphaDegree = alphaRadian * (180.0 / Math.PI);

                                double betaDegree = 135.0; 
                                double thetaDegree = alphaDegree - betaDegree;
                                double thetaRadian = thetaDegree * (Math.PI / 180.0);

                                if (thetaRadian != 0)
                                {
                                    Line axisZ = Line.CreateBound(center, center + XYZ.BasisZ);
                                    ElementTransformUtils.RotateElement(_doc, newFitting.Id, axisZ, thetaRadian);
                                }
                            }
                        }
                        else
                        {
                            double angleRadian = fittingData.Angle * (Math.PI / 180.0);
                            if (angleRadian != 0)
                            {
                                Line axis = Line.CreateBound(location, location + XYZ.BasisZ);
                                ElementTransformUtils.RotateElement(_doc, newFitting.Id, axis, angleRadian);
                            }
                        }

                        idMap[fittingData.Id] = newFitting;
                    }
                }

                foreach (var pipeData in exportDoc.Pipes)
                {
                    XYZ startXYZ = ParseXYZ(pipeData.StartPoint);
                    XYZ endXYZ = ParseXYZ(pipeData.EndPoint);

                    ElementId pipeLevelId = targetLevelId != ElementId.InvalidElementId ? targetLevelId : (allLevels.FirstOrDefault(l => l.Name == pipeData.LevelName)?.Id ?? fallbackLevelId);
                    ElementId currentPipeTypeId = allPipeTypes.FirstOrDefault(x => x.Name == pipeData.PipeTypeName)?.Id ?? allPipeTypes.FirstOrDefault()?.Id;
                    ElementId currentSystemTypeId = allSystemTypes.FirstOrDefault(x => x.Name == pipeData.SystemTypeName)?.Id ?? allSystemTypes.FirstOrDefault()?.Id;

                    if (currentPipeTypeId == null || currentSystemTypeId == null) continue;

                    Pipe newPipe = Pipe.Create(_doc, currentSystemTypeId, currentPipeTypeId, pipeLevelId, startXYZ, endXYZ);

                    Parameter diameterParam = newPipe.get_Parameter(BuiltInParameter.RBS_PIPE_DIAMETER_PARAM);
                    if (diameterParam != null && !diameterParam.IsReadOnly)
                    {
                        diameterParam.Set(pipeData.Diameter);
                    }

                    idMap[pipeData.Id] = newPipe;
                }

                _doc.Regenerate();

                // 3. KẾT NỐI CONNECTOR
                foreach (var pipeData in exportDoc.Pipes)
                {
                    if (!idMap.ContainsKey(pipeData.Id)) continue;
                    Element currentPipe = idMap[pipeData.Id];

                    if (pipeData.Connectors != null)
                    {
                        foreach (var connData in pipeData.Connectors)
                        {
                            if (!string.IsNullOrEmpty(connData.ConnectedToId) && idMap.ContainsKey(connData.ConnectedToId))
                            {
                                Element targetElement = idMap[connData.ConnectedToId];
                                XYZ jointLocation = ParseXYZ(connData.Origin);

                                Connector c1 = GetClosestConnector(currentPipe, jointLocation);
                                Connector c2 = GetClosestConnector(targetElement, jointLocation);

                                if (c1 != null && c2 != null && !c1.IsConnectedTo(c2))
                                {
                                    try
                                    {
                                        c1.ConnectTo(c2);
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                }

                t.Commit();
            }
        }
    }
}