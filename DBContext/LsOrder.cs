namespace SiteDeploy.Document.DBContext
{
    public class LsOrder
    {
        public long Id { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public bool IsTestMode { get; set; }
        public bool IsRefunded { get; set; }
        public string RawPayload { get; set; } = string.Empty;
        public bool IsEmailSend { get; set; } = false;
        public DateTime EmailSentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
