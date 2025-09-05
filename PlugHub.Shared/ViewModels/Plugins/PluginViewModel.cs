using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PlugHub.Shared.Extensions;
using PlugHub.Shared.ViewModels;
using System.Diagnostics;
using System.Reflection;

namespace PlugHub.ViewModels
{
    public partial class PluginViewModel : ObservableObject
    {
        public bool? IsEnabled
        {
            get => this.isEnabled;
            set
            {
                if (this.isEnabled != value)
                {
                    this.isEnabled = value;
                    this.OnPropertyChanged(nameof(this.IsEnabled));

                    IsEnabledChanged?.Invoke(this);
                }
            }
        }
        private bool? isEnabled = false;
        public event Action<PluginViewModel>? IsEnabledChanged;


        [ObservableProperty]
        private Guid pluginID = Guid.Empty;

        [ObservableProperty]
        private string assemblyFile = string.Empty;

        [ObservableProperty]
        private string assemblyName = string.Empty;

        [ObservableProperty]
        private string typeName = string.Empty;

        [ObservableProperty]
        private string iconSource = string.Empty;

        [ObservableProperty]
        private string name = "Unknown";

        [ObservableProperty]
        private string description = string.Empty;

        [ObservableProperty]
        private string version = string.Empty;

        [ObservableProperty]
        private string author = string.Empty;

        [ObservableProperty]
        private IEnumerable<string> categories = [];

        [ObservableProperty]
        private string docsLink = string.Empty;

        [ObservableProperty]
        private string supportLink = string.Empty;

        [ObservableProperty]
        private string supportContact = string.Empty;

        [ObservableProperty]
        private string changeLog = string.Empty;

        [ObservableProperty]
        private IEnumerable<PluginDescriptorViewModel> providedDescriptors = [];


        public Type PluginType = typeof(object);

        public bool HasNoInterfaces => !this.ProvidedDescriptors.Any();

        public string PluginFullName => $"{this.AssemblyName}:{this.TypeName}";


        public bool HasImageIcon =>
            !string.IsNullOrEmpty(this.IconSource) && this.IconSource.StartsWith("avares:", StringComparison.OrdinalIgnoreCase);
        public bool ShowDefaultIcon =>
            string.IsNullOrEmpty(this.IconSource);


        public bool HasDocsLink =>
            !string.IsNullOrWhiteSpace(this.DocsLink);
        public bool HasSupportLink =>
            !string.IsNullOrWhiteSpace(this.SupportLink);
        public bool HasSupportContact =>
            !string.IsNullOrWhiteSpace(this.SupportContact);
        public bool HasChangeLog =>
            !string.IsNullOrWhiteSpace(this.ChangeLog);


        public static PluginViewModel CreateFromPluginType(Type pluginType, bool isEnabled = false)
        {
            ArgumentNullException.ThrowIfNull(pluginType);

            IDictionary<string, object?> staticProps = pluginType.GetStaticProperties();

            PluginViewModel vm = new();
            Type vmType = typeof(PluginViewModel);

            foreach (PropertyInfo prop in vmType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prop.CanWrite)
                    continue;

                bool isEnable =
                    string.Equals(
                        prop.Name,
                        nameof(PluginViewModel.IsEnabled),
                        StringComparison.OrdinalIgnoreCase);

                foreach (KeyValuePair<string, object?> kvp in staticProps)
                {
                    if (string.Equals(kvp.Key, prop.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        object? value = kvp.Value;

                        if (value == null)
                        {
                            if (!prop.PropertyType.IsValueType || Nullable.GetUnderlyingType(prop.PropertyType) != null)
                                prop.SetValue(vm, null);
                        }
                        else if (prop.PropertyType.IsAssignableFrom(value.GetType()))
                        {
                            prop.SetValue(vm, value);
                        }
                        else
                        {
                            try
                            {
                                Type targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                                object converted = Convert.ChangeType(value, targetType);

                                prop.SetValue(vm, converted);
                            }
                            catch { /* Nothing to see here. */ }
                        }
                        break;
                    }
                }
            }

            vm.PluginType = pluginType;
            vm.IsEnabled = isEnabled;
            vm.TypeName = pluginType.FullName ?? "Missing";
            vm.AssemblyName = pluginType.Assembly.GetName().Name ?? "Missing";
            vm.AssemblyFile = string.IsNullOrEmpty(pluginType.Assembly.Location)
                ? pluginType.Assembly.GetName().Name + ".dll"
                : Path.GetFileName(pluginType.Assembly.Location);

            return vm;
        }


        [RelayCommand]
        private void OpenDocumentation()
        {
            if (!string.IsNullOrWhiteSpace(this.DocsLink))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = this.DocsLink,
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        private void OpenSupport()
        {
            if (!string.IsNullOrWhiteSpace(this.SupportLink))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = this.SupportLink,
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        private void OpenContact()
        {
            if (!string.IsNullOrWhiteSpace(this.SupportContact))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = $"mailto:{this.SupportContact}",
                    UseShellExecute = true
                });
            }
        }

        [RelayCommand]
        private void OpenChangeLog()
        {
            if (!string.IsNullOrWhiteSpace(this.ChangeLog))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = this.ChangeLog,
                    UseShellExecute = true
                });
            }
        }
    }
}
