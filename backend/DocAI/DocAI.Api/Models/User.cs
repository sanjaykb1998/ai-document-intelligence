namespace DocAI.Api.Models
{
    public class User
    {
        public Guid Id { get; set; }

        public string Username { get; set; }

        public string Email { get; set; }

        public string PasswordHash { get; set; }

        public ICollection<Document> Documents { get; set; }
    }
}
