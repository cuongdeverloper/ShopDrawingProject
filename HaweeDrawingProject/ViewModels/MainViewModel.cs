using Autodesk.Revit.DB;
using HaweeDrawingProject.Commands;
using HaweeDrawingProject.Models;
using HaweeDrawingProject.Services;
using Microsoft.Win32;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace HaweeDrawingProject.ViewModels
{
    public class LevelItem
    {
        public string Name { get; set; }
        public ElementId LevelId { get; set; }
    }

    public class MainViewModel : INotifyPropertyChanged
    {
        private RevitPipeService _pipeService;
        private Document _doc;

        public List<LevelItem> Levels { get; set; }

        private LevelItem _selectedLevel;
        public LevelItem SelectedLevel
        {
            get { return _selectedLevel; }
            set { _selectedLevel = value; OnPropertyChanged(nameof(SelectedLevel)); }
        }

        public ICommand ExportCommand { get; set; }
        public ICommand ImportCommand { get; set; }

        public MainViewModel(Document doc)
        {
            _doc = doc;
            _pipeService = new RevitPipeService(_doc);

            Levels = _pipeService.GetLevelItems();

            if (Levels.Count > 0)
                SelectedLevel = Levels[0];

            ExportCommand = new RelayCommand(ExecuteExport, CanExecuteAction);
            ImportCommand = new RelayCommand(ExecuteImport, CanExecuteAction);
        }

        private bool CanExecuteAction(object obj)
        {
            return SelectedLevel != null;
        }

        private void ExecuteExport(object obj)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (saveFileDialog.ShowDialog() == true)
            {
                _pipeService.ExportPipesToJson(SelectedLevel.LevelId, saveFileDialog.FileName);
                MessageBox.Show("Export thành công!");
            }
        }

        private void ExecuteImport(object obj)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "JSON files (*.json)|*.json" };
            if (openFileDialog.ShowDialog() == true)
            {
                ImportResult result = _pipeService.ImportPipesFromJson(openFileDialog.FileName, SelectedLevel.LevelId);

                // Build a short summary for the user
                string summary = $"Import hoàn tất.\nPipes created: {result.PipesCreated}\nFittings created: {result.FittingsCreated}\nErrors: {result.Errors.Count}\n\n";
                if (result.Messages.Count > 0)
                {
                    summary += "Notes:\n" + string.Join("\n", result.Messages);
                }

                MessageBox.Show(summary, "Import Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}