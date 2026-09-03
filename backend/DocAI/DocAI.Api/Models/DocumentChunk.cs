namespace DocAI.Api.Models
{
    public class DocumentChunk
    {
        public Guid DocumentId { get; set; }

        public Guid UserId { get; set; }

        public string FileName { get; set; }

        public int ChunkIndex { get; set; }

        public string Text { get; set; }

        public float[] Embedding { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
