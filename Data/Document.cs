using System.ComponentModel.DataAnnotations;

namespace MaestroNotes.Data
{
    public class Document
    {
        [Key]
        public int Id { get; set; } = 0;
        [MaxLength(250)]
        public string FileName { get; set; } = "";
        [MaxLength(250)]
        public string EncryptedName { get; set; } = "";
        public DocumentType DocumentType { get; set; } = DocumentType.Pdf;
        public int MusicRecordId { get; set; } = 0;

        public bool Vormerken { get; set; } = false;

        // ALTER TABLE Documents ADD Vormerken BOOLEAN NOT NULL DEFAULT 0;
    }
}
