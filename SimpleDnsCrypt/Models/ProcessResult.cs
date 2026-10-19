namespace SimpleDnsCrypt.Models
{
    public class ProcessResult
    {
        public bool Success { get; set; }
        public string StandardError { get; set; } = string.Empty;
        public string StandardOutput { get; set; } = string.Empty;
    }
}
