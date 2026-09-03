namespace DocAI.Api.Models
{
    public class SearchResult
    {
        public Guid DocumentId { get; set; }

        public string FileName { get; set; }

        public int ChunkIndex { get; set; }

        public string Text { get; set; }

        public double Score { get; set; }
    }
}
