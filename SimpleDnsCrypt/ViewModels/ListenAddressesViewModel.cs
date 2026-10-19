using Caliburn.Micro;
using SimpleDnsCrypt.Config;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using SimpleDnsCrypt.Helper;

namespace SimpleDnsCrypt.ViewModels
{
    [Export(typeof(ListenAddressesViewModel))]
    public class ListenAddressesViewModel : Screen
    {
        private string _windowTitle = string.Empty;
        private ObservableCollection<string>? _listenAddresses;
        private string? _selectedListenAddress;
        private string _addressInput = string.Empty;


        [ImportingConstructor]
        public ListenAddressesViewModel()
        {
            _listenAddresses = new ObservableCollection<string>();
        }

        /// <summary>
        ///     The title of the window.
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            set
            {
                _windowTitle = value;
                NotifyOfPropertyChange(() => WindowTitle);
            }
        }

        public ObservableCollection<string>? ListenAddresses
        {
            get => _listenAddresses;
            set
            {
                _listenAddresses = value;
                NotifyOfPropertyChange(() => ListenAddresses);
            }
        }

        public string? SelectedListenAddress
        {
            get => _selectedListenAddress;
            set
            {
                _selectedListenAddress = value;
                NotifyOfPropertyChange(() => SelectedListenAddress);
            }
        }

        public string AddressInput
        {
            get => _addressInput;
            set
            {
                _addressInput = value;
                NotifyOfPropertyChange(() => AddressInput);
            }
        }

        public void AddAddress()
        {
            var validatedAddress = ValidationHelper.ValidateIpEndpoint(_addressInput);
            if (string.IsNullOrEmpty(validatedAddress)) return;
            _listenAddresses ??= new ObservableCollection<string>();
            if (_listenAddresses.Contains(validatedAddress)) return;
            _listenAddresses.Add(validatedAddress);
            AddressInput = string.Empty;
        }

        public void RemoveAddress()
        {
            if (string.IsNullOrEmpty(_selectedListenAddress)) return;
            if (_listenAddresses == null) return;
            if (_listenAddresses.Count == 1) return;
            _listenAddresses.Remove(_selectedListenAddress);
        }

        public void RestoreDefault()
        {
            _listenAddresses ??= new ObservableCollection<string>();
            _listenAddresses.Clear();
            _listenAddresses.Add(Global.DefaultResolverIpv4);
            _listenAddresses.Add(Global.DefaultResolverIpv6);
        }
    }
}
