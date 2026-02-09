using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace MaestroNotes.Data
{
    [Index(nameof(Name), IsUnique = true)]
    [Index(nameof(Email), IsUnique = true)]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(12)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(60)]
        public string Email { get; set; } = string.Empty;

        public UserLevel UserLevel { get; set; } = UserLevel.Viewer;
    }
}
