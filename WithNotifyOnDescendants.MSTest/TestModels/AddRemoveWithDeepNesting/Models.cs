using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WithNotifyOnDescendants.Proto.MSTest.TestModels.AddRemoveWithDeepNesting
{
    class ModelGO : INotifyPropertyChanged
    {
        public string Guid
        {
            get => _guid;
            set
            {
                if (!Equals(_guid, value))
                {
                    _guid = value;
                    OnPropertyChanged();
                }
            }
        }
        string _guid = string.Empty;

        public object? CompleteUnknown
        {
            get => _completeUnknown;
            set
            {
                if (!Equals(_completeUnknown, value))
                {
                    _completeUnknown = value;
                    OnPropertyChanged();
                }
            }
        }
        object? _completeUnknown = null;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;

    }
    class ModelLevel1GO : INotifyPropertyChanged
    {
        public string Guid
        {
            get => _guid;
            set
            {
                if (!Equals(_guid, value))
                {
                    _guid = value;
                    OnPropertyChanged();
                }
            }
        }
        string _guid = string.Empty;

        public object? CompleteUnknown
        {
            get => _completeUnknown;
            set
            {
                if (!Equals(_completeUnknown, value))
                {
                    _completeUnknown = value;
                    OnPropertyChanged();
                }
            }
        }
        object? _completeUnknown = null;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;

    }
    class ModelLevel2GO : INotifyPropertyChanged
    {
        public string Guid
        {
            get => _guid;
            set
            {
                if (!Equals(_guid, value))
                {
                    _guid = value;
                    OnPropertyChanged();
                }
            }
        }
        string _guid = string.Empty;

        public object? CompleteUnknown
        {
            get => _completeUnknown;
            set
            {
                if (!Equals(_completeUnknown, value))
                {
                    _completeUnknown = value;
                    OnPropertyChanged();
                }
            }
        }
        object? _completeUnknown = null;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        public event PropertyChangedEventHandler? PropertyChanged;

    }
}
