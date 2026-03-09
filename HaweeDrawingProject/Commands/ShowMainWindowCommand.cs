using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using HaweeDrawingProject.ViewModels;
using HaweeDrawingProject.Views;

namespace HaweeDrawingProject.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class ShowMainWindowCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;

            MainViewModel viewModel = new MainViewModel(doc);
            MainView view = new MainView();
            view.DataContext = viewModel;

            view.ShowDialog();

            return Result.Succeeded;
        }
    }
}