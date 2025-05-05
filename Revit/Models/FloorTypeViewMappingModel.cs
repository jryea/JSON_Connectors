using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit.Export.Models
{
    public class FloorTypeViewMappingModel : INotifyPropertyChanged
    {
        private string _floorTypeName;
        private string _floorTypeId;
        private ViewPlanViewModel _selectedViewPlan;

        public string FloorTypeName
        {
            get => _floorTypeName;
            set
            {
                _floorTypeName = value;
                OnPropertyChanged();
            }
        }

        public string FloorTypeId
        {
            get => _floorTypeId;
            set
            {
                _floorTypeId = value;
                OnPropertyChanged();
            }
        }

        public ViewPlanViewModel SelectedViewPlan
        {
            get => _selectedViewPlan;
            set
            {
                _selectedViewPlan = value;
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