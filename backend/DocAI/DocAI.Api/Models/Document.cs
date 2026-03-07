namespace DocAI.Api.Models
{
    public class Document
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public DateTime UploadedAt { get; set; }
        public string Status { get; set; }
        public string? ExtractedText { get; set; }
        public string? Summary { get; set; }

        public Guid UserId { get; set; }
        public User User { get; set; }
    }
}
