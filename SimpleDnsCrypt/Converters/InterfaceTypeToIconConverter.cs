using System;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Windows.Data;
using SimpleDnsCrypt.Models;

namespace SimpleDnsCrypt.Converters
{
    /// <summary>
    ///     Interface to icon converter.
    /// </summary>
    public class InterfaceTypeToIconConverter : IValueConverter
    {
        public object EthernetIconOffline { get; set; } = string.Empty;
        public object EthernetIcon { get; set; } = string.Empty;
        public object WifiIcon { get; set; } = string.Empty;
        public object WifiIconOffline { get; set; } = string.Empty;


        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                var localNetworkInterface = (LocalNetworkInterface)value;

                // A Hyper-V switch reports NetworkInterfaceType.Ethernet, so the switch below would
                // hand it the same cable icon the Realtek adapter gets. The marker decides the icon
                // before the type does, because the icon is the only place on the tile that is free:
                // Tile.Title owns the bottom-left and the address line the top-left.
                if (localNetworkInterface.IsVirtualAdapter)
                {
                    return "CubeOutline";
                }

                bool isCable;
                switch (localNetworkInterface.Type)
                {
                    case NetworkInterfaceType.Ethernet:
                    case NetworkInterfaceType.Ethernet3Megabit:
                    case NetworkInterfaceType.FastEthernetFx:
                    case NetworkInterfaceType.FastEthernetT:
                    case NetworkInterfaceType.GigabitEthernet:
                        isCable = true;
                        break;
                    case NetworkInterfaceType.Wireless80211:
                        isCable = false;
                        break;
                    default:
                        isCable = true;
                        break;
                }

                if (isCable)
                {
                    return localNetworkInterface.OperationalStatus != OperationalStatus.Up ? EthernetIconOffline : EthernetIcon;
                }
                return localNetworkInterface.OperationalStatus != OperationalStatus.Up ? WifiIconOffline : WifiIcon;
            }
            catch (Exception)
            {
                return EthernetIcon;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
