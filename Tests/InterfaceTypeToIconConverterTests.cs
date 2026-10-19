using System;
using System.Globalization;
using System.Net.NetworkInformation;
using NUnit.Framework;
using SimpleDnsCrypt.Converters;
using SimpleDnsCrypt.Models;

namespace Tests
{
    /// <summary>
    /// The 40px icon is the only thing a user reads before clicking an adapter, and it has been
    /// saying "cable" for a Hyper-V switch ever since that switch started reporting
    /// NetworkInterfaceType.Ethernet.
    ///
    /// Marking it anywhere else on the tile was measured and rejected: MahApps draws Tile.Title
    /// across the bottom-left of the 100px band and the address line already owns the top-left, so
    /// a corner badge would sit under a long adapter name. The icon is the only slot that is free.
    /// </summary>
    public class InterfaceTypeToIconConverterTests
    {
        private static readonly CultureInfo En = new CultureInfo("en", false);

        private static InterfaceTypeToIconConverter Converter() => new InterfaceTypeToIconConverter
        {
            EthernetIcon = "EthernetCable",
            EthernetIconOffline = "EthernetCableOff",
            WifiIcon = "Wifi",
            WifiIconOffline = "WifiOff"
        };

        private static LocalNetworkInterface Card(NetworkInterfaceType type, OperationalStatus status, bool isVirtual)
        {
            return new LocalNetworkInterface
            {
                Name = "adapter",
                Type = type,
                OperationalStatus = status,
                IsVirtualAdapter = isVirtual
            };
        }

        [Test]
        public void AVirtualAdapterIsDrawnAsACubeEvenWhenItsTypeSaysEthernet()
        {
            var icon = Converter().Convert(
                Card(NetworkInterfaceType.Ethernet, OperationalStatus.Up, isVirtual: true),
                typeof(string), null!, En);

            Assert.AreEqual("CubeOutline", icon,
                "a Hyper-V or Bluetooth adapter rendered with the cable icon, which is the whole " +
                "confusion the badge exists to end");
        }

        [Test]
        public void APhysicalAdapterKeepsTheCableIcon()
        {
            var icon = Converter().Convert(
                Card(NetworkInterfaceType.Ethernet, OperationalStatus.Up, isVirtual: false),
                typeof(string), null!, En);

            Assert.AreEqual("EthernetCable", icon);
        }

        [Test]
        public void ADisconnectedPhysicalAdapterStillShowsItIsDisconnected()
        {
            // The offline variants are what tell a user their cable is unplugged; routing every
            // virtual adapter to the cube must not quietly swallow that for the physical ones.
            var icon = Converter().Convert(
                Card(NetworkInterfaceType.Wireless80211, OperationalStatus.Down, isVirtual: false),
                typeof(string), null!, En);

            Assert.AreEqual("WifiOff", icon);
        }
    }
}
