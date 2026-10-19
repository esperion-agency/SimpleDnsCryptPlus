using System;

namespace SimpleDnsCrypt.Models
{
    public class AddressBlockLogLine : LogLine
    {
        public DateTime Time { get; set; }
        // The constructor below does not parse the line yet, so every field stays null.
        public string? Host { get; set; }
        public string? QName { get; set; }
        public string? Message { get; set; }

        public AddressBlockLogLine(string line)
        {
        }
    }
}