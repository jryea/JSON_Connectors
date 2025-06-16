using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace Revit.Models
{
    public class SheetViewModel : INotifyPropertyChanged
    {
        private string _sheetNumber;
        private string _sheetName;
        private string _viewName;
        private ElementId _viewId;
        private ElementId _sheetId;
        private bool _isSelected;
        private FloorTypeModel _selectedFloorType;

        public string SheetNumber
        {
            get => _sheetNumber;
            set
            {
                _sheetNumber = value;
                OnPropertyChanged();
            }
        }

        public string SheetName
        {
            get => _sheetName;
            set
            {
                _sheetName = value;
                OnPropertyChanged();
            }
        }

        public string ViewName
        {
            get => _viewName;
            set
            {
                _viewName = value;
                OnPropertyChanged();
            }
        }

        public ElementId ViewId
        {
            get => _viewId;
            set
            {
                _viewId = value;
                OnPropertyChanged();
            }
        }

        public ElementId SheetId
        {
            get => _sheetId;
            set
            {
                _sheetId = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public FloorTypeModel SelectedFloorType
        {
            get => _selectedFloorType;
            set
            {
                _selectedFloorType = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}