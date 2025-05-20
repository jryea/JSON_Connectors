using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace Revit.Export.Models
{
    public class LevelViewModel : INotifyPropertyChanged
    {
        private string _id;
        private string _name;
        private double _elevation;
        private FloorTypeModel _selectedFloorType;
        private bool _isSelected;
        private ElementId _levelId;
        private bool _isMasterStory;
        private LevelViewModel _similarToLevel;
        private bool _isEnabledForExport = true;

        public string Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged();
            }
        }

        public string Name
        {
            get => _name;
            set
            {
                _name = value;
                OnPropertyChanged();
            }
        }

        public double Elevation
        {
            get => _elevation;
            set
            {
                _elevation = value;
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

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged();

                    // When selection changes, notify the view model to update master story list
                    if (PropertyChanged != null)
                    {
                        OnPropertyChanged(nameof(IsMasterStory)); // Trigger update of dependent properties
                    }
                }
            }
        }

        public ElementId LevelId
        {
            get => _levelId;
            set
            {
                _levelId = value;
                OnPropertyChanged();
            }
        }

        public bool IsMasterStory
        {
            get => _isMasterStory;
            set
            {
                if (_isMasterStory != value)
                {
                    _isMasterStory = value;
                    OnPropertyChanged();

                    // Notify that master story status changed (for updating collection)
                    NotifyMasterStoryChanged();
                }
            }
        }

        public LevelViewModel SimilarToLevel
        {
            get => _similarToLevel;
            set
            {
                _similarToLevel = value;
                OnPropertyChanged();
            }
        }

        public bool IsEnabledForExport
        {
            get => _isEnabledForExport;
            set
            {
                _isEnabledForExport = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void NotifyMasterStoryChanged()
        {
            // This is a way to notify the parent view model that it should update its master story list
            // The actual implementation would depend on your architecture
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("MasterStoryCollectionChanged"));
        }
    }
}