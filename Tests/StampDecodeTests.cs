using NUnit.Framework;
using SimpleDnsCrypt.Utils;
using SimpleDnsCrypt.Utils.Models;

namespace Tests
{
    public class StampDecodeTests
    {
        [Test]
        public void StampDecodeTest1()
        {
            const string stamp = "sdns://AQcAAAAAAAAADjIxMi40Ny4yMjguMTM2IOgBuE6mBr-wusDOQ0RbsV66ZLAvo8SqMa4QY2oHkDJNHzIuZG5zY3J5cHQtY2VydC5mci5kbnNjcnlwdC5vcmc";
            var result = StampTools.Decode(stamp);
            Assert.That(result, Is.Not.Null);
            Assert.AreEqual(StampProtocol.DnsCrypt, result!.Protocol);
            Assert.AreEqual("2.dnscrypt-cert.fr.dnscrypt.org", result.ProviderName);
        }

        [Test]
        public void StampDecodeTest2()
        {
            const string stamp = "sdns://AgcAAAAAAAAADTM3LjU5LjIzOC4yMTMgwzRA_TfjYt0RwSHqBHwj7OM-D_x-CDgqIHeJHIoN1P0UZG9oLmZyLmRuc2NyeXB0LmluZm8KL2Rucy1xdWVyeQ";
            var result = StampTools.Decode(stamp);
            Assert.That(result, Is.Not.Null);
            Assert.AreEqual(StampProtocol.DoH, result!.Protocol);
            Assert.AreEqual("doh.fr.dnscrypt.info", result.Hostname);
        }

        [Test]
        public void StampDecodeTest3()
        {
            const string stamp = "sdns://AgUAAAAAAAAAACDyXGrcc5eNecJ8nomJCJ-q6eCLTEn6bHic0hWGUwYQaA5kbnMuZ29vZ2xlLmNvbQ0vZXhwZXJpbWVudGFs";
            var result = StampTools.Decode(stamp);
            Assert.That(result, Is.Not.Null);
            Assert.AreEqual(StampProtocol.DoH, result!.Protocol);
            Assert.AreEqual("dns.google.com", result.Hostname);
        }

        [Test]
        public void StampDecodeTestRelay()
        {
            const string stamp = "sdns://gQ84My43Ny44NS43Ojg0NDM";
            var result = StampTools.Decode(stamp);
            Assert.That(result, Is.Not.Null);
            Assert.AreEqual(StampProtocol.DNSCryptRelay, result!.Protocol);
            Assert.AreEqual("83.77.85.7", result.Address);
        }

        /// <summary>
        ///     The five shapes <see cref="StampTools.Decode"/> gives up on: no prefix, empty payload, and a
        ///     protocol whose required fields are truncated. Each case first proves <c>Decode</c> reaches
        ///     null without throwing - otherwise a red here would be a crash, not the defect - and only then
        ///     asks <c>TryDecode</c> to admit it.
        /// </summary>
        [TestCase("this is not a stamp at all")]
        [TestCase("sdns://")]
        [TestCase("sdns://AQ")]
        [TestCase("sdns://Ag")]
        [TestCase("sdns://gQ")]
        public void TryDecode_StampThatDecodesToNothing_ReportsFailure(string stamp)
        {
            Assert.That(StampTools.Decode(stamp), Is.Null,
                $"'{stamp}' must be refused by Decode returning null, not by an exception");

            Assert.IsFalse(StampTools.TryDecode(stamp, out var decoded),
                $"'{stamp}' decoded to nothing, so TryDecode must report failure");
            Assert.That(decoded, Is.Null);
        }

        [Test]
        public void TryDecode_ValidStamp_ReportsSuccess()
        {
            const string stamp = "sdns://AQcAAAAAAAAADjIxMi40Ny4yMjguMTM2IOgBuE6mBr-wusDOQ0RbsV66ZLAvo8SqMa4QY2oHkDJNHzIuZG5zY3J5cHQtY2VydC5mci5kbnNjcnlwdC5vcmc";

            Assert.IsTrue(StampTools.TryDecode(stamp, out var decoded),
                "a stamp that decodes must not be reported as a failure");
            Assert.That(decoded, Is.Not.Null);
            Assert.AreEqual(StampProtocol.DnsCrypt, decoded!.Protocol);
        }

    }
}
