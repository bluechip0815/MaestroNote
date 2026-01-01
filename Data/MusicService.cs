using Serilog;

namespace MaestroNotes.Data
{
    public class MusicService
    {
        private readonly MusicContext _context;
        public MusicService(MusicContext context)
        {
            _context = context;
            try
            {
                DefaultLists.Init(context);
                Log.Logger.Information("MusicService constructed");
            }
            catch (Exception ex) 
            {
                Log.Logger.Error(ex.Message);
            }
        }
        public AccessType EnablerTest(string pw)
        {
            if (pw.Equals(_context.PasswordRO))
                return AccessType.ReadOnly;
            return pw.Equals(_context.PasswordRW) ? AccessType.ReadWrite : AccessType.None;
        }
        public List<MusicRecord> GetAllMusicRecords()
        {
            return _context.MusicRecords.ToList();
        }
        public async Task<bool> SaveDataSet(MusicRecord record)
        {
            try
            {
                if (record.Id == 0)
                    _context.MusicRecords.Add(record);
                else
                    _context.MusicRecords.Update(record);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                return false;
            }
        }
        public async Task DeleteDataSet(int id)
        {
            try
            {
                var record = _context.MusicRecords.Find(id);
                if (record != null)
                {
                    _context.MusicRecords.Remove(record);
                    await _context.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }
        }
        public MusicRecord? GetRecordById(int id)
        {
            return _context.MusicRecords.Find(id);
        }
        public MusicRecord CreateNewRecord()
        {
            return new MusicRecord();
        }

        /// <summary>
        /// Documents  
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public bool HasDocuments(int id)
        {
            try
            {
                return _context.Documents.Count(m => m.MusicRecordId == id)>0;
            }
            catch  (Exception ex) 
            {
                Log.Logger.Error(ex.Message);
            }
            return false;
        }
        public record DOC { public string fn = ""; public int id = 0; public string path = ""; };
        public List<DOC> GetDocuments(int id)
        {
            return GetAllDocuments(id, DocumentType.Pdf);
        }
        public List<DOC> GetImages(int id)
        {
            return GetAllDocuments(id, DocumentType.Image);
        }
        private List<DOC> GetAllDocuments(int id, DocumentType type)
        {
            List<DOC> lst = new();
            try
            {
                List<Document> docs = _context.Documents.Where(m => m.MusicRecordId == id && m.DocumentType == type).ToList();
                if (docs is not null && docs.Any())
                {
                    string folder = type == DocumentType.Pdf ? GetDocumentPath() : GetImagePath();

                    folder = folder.Replace("wwwroot/", "");

                    foreach (Document d in docs)
                    {
                        lst.Add(new DOC()
                        {
                            fn = d.FileName.Substring(0, d.FileName.LastIndexOf('.')),
                            path = Path.Combine(folder, d.EncryptedName),
                            id = d.Id
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
            }
            return lst;
        }
        public async Task<string> SaveFile(int pid, string FileName, byte[] fileBytes, DocumentType type)
        {
            if (_context is null)
                return "Database cintext error";

            try
            {
                string efn = Guid.NewGuid().ToString();
                if (type == DocumentType.Pdf)
                    efn += ".pdf";
                else
                {
                    string etx = FileName.Substring(FileName.LastIndexOf("."));
                    efn += etx;
                }
                Document document = new()
                {
                    FileName = FileName,
                    EncryptedName = efn,
                    DocumentType = type,
                    MusicRecordId = pid
                };
                string path = type == DocumentType.Pdf ? GetDocumentPath() : GetImagePath();
                string fn = Path.Combine(path, document.EncryptedName);
                if (await SaveDocuDataSet(document))
                {
                    Log.Logger.Information($"Save {fn}");
                    File.WriteAllBytes(fn, fileBytes);
                    return FileName + " gespeichert";
                }
                else
                {
                    return FileName + " konnte nicht gespeichert werden.";
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex.Message);
                return ex.Message;
            }
        }
        public async Task<bool> SaveDocuDataSet(Document record)
        {
            try
            {
                if (record.Id == 0)
                    _context.Documents.Add(record);
                else
                    _context.Documents.Update(record);
                await _context.SaveChangesAsync();
                return true;
            }
            catch (Exception e)
            {
                Log.Logger.Error(e.Message);
                return false;
            }
        }
        public async Task<string> DeleteDocument(int id)
        {
            try {
                var record = _context.Documents.Find(id);
                if (record == null)
                    return $"Dataset #{id} not found";

                _context.Documents.Remove(record);
                await _context.SaveChangesAsync();

                string path = record.DocumentType == DocumentType.Pdf ? GetDocumentPath() : GetImagePath();
                File.Delete(Path.Combine(path, record.EncryptedName));

                return $"Dataset #{id} removed";
            }
            catch (Exception ex) 
            {
                Log.Logger.Error(ex.Message);
                return ex.Message;
            }
        }
        public string GetDocumentPath()
        {
            // ./Documents/
            return _context is not null ? _context.DocumentsPath : "";
        }
        public string GetImagePath()
        {
            // ./Documents/
            return _context is not null ? _context.ImagesPath : "";
        }
    }
}
