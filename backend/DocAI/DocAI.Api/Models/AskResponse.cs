namespace DocAI.Api.Models
{
    public class AskResponse
    {
        public string Answer { get; set; }

        public IReadOnlyList<SearchResult> Sources { get; set; }
    }
}
