using System.ComponentModel.DataAnnotations;

namespace MaestroNotes.Data
{
    public class LoginToken
    {
        [Key]
        public int Id { get; set; }

        public Guid Token { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(12)] // Match User.Name
        public string UserName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsUsed { get; set; } = false;
    }
}
