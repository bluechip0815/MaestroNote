using Serilog;
using Microsoft.EntityFrameworkCore;

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

        public List<MusicRecordDisplayDto> GetDisplayRecords(string category, string searchTerm, DateTime? dateFrom, DateTime? dateTo, int limit)
        {
            var query = _context.MusicRecords
                .Include(m => m.Dirigent)
                .Include(m => m.Orchester)
                .Include(m => m.OrtEntity)
                .Include(m => m.Werke).ThenInclude(w => w.Komponist)
                .Include(m => m.Solisten)
                .AsQueryable();

            if (!string.IsNullOrEmpty(category))
            {
                switch (category)
                {
                    case "Werk":
                        if (!string.IsNullOrEmpty(searchTerm))
                            query = query.Where(m => m.Werke.Any(w => w.Name == searchTerm));
                        break;
                    case "Komponist":
                        if (!string.IsNullOrEmpty(searchTerm))
                            query = query.Where(m => m.Werke.Any(w => w.Komponist != null && (w.Komponist.Name + (string.IsNullOrEmpty(w.Komponist.Vorname) ? "" : ", " + w.Komponist.Vorname)) == searchTerm));
                        break;
                    case "Dirigent":
                        if (!string.IsNullOrEmpty(searchTerm))
                            query = query.Where(m => m.Dirigent != null && (m.Dirigent.Name + (string.IsNullOrEmpty(m.Dirigent.Vorname) ? "" : ", " + m.Dirigent.Vorname)) == searchTerm);
                        break;
                    case "Solist":
                        if (!string.IsNullOrEmpty(searchTerm))
                            query = query.Where(m => m.Solisten.Any(s => (s.Name + (string.IsNullOrEmpty(s.Vorname) ? "" : ", " + s.Vorname)) == searchTerm));
                        break;
                    case "Orchester":
                        if (!string.IsNullOrEmpty(searchTerm))
                            query = query.Where(m => m.Orchester != null && m.Orchester.Name == searchTerm);
                        break;
                    case "Ort":
                        if (!string.IsNullOrEmpty(searchTerm))
                            query = query.Where(m => m.OrtEntity != null && m.OrtEntity.Name == searchTerm);
                        break;
                    case "Saisson":
                        if (!string.IsNullOrEmpty(searchTerm))
                            query = query.Where(m => m.Spielsaison == searchTerm);
                        break;
                    case "Note":
                        if (!string.IsNullOrEmpty(searchTerm))
                        {
                            string pattern = searchTerm.Replace("*", "%").Replace("?", "_");
                            query = query.Where(m => EF.Functions.Like(m.Bewertung, pattern));
                        }
                        break;
                    case "Datum":
                        if (dateFrom.HasValue)
                            query = query.Where(m => m.Datum >= dateFrom.Value);
                        if (dateTo.HasValue)
                            query = query.Where(m => m.Datum <= dateTo.Value);
                        break;
                }
            }

            query = query.OrderByDescending(m => m.Datum);

            if (limit > 0)
            {
                query = query.Take(limit);
            }

            var entities = query.ToList();

            return entities.Select(m => new MusicRecordDisplayDto
            {
                Id = m.Id,
                Datum = m.Datum,
                Spielsaison = m.Spielsaison,
                Ort = m.OrtEntity?.Name ?? m.Ort,
                Bewertung = m.Bewertung,
                KomponistNames = string.Join(", ", m.Werke.Select(w => w.Komponist?.Name ?? "").Where(s => !string.IsNullOrEmpty(s))),
                WerkNames = string.Join(", ", m.Werke.Select(w => w.Name)),
                OrchesterName = m.Orchester?.Name ?? "",
                DirigentName = m.Dirigent?.Name ?? "",
                SolistNames = string.Join(", ", m.Solisten.Select(s => s.Name))
            }).ToList();
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
            return _context.MusicRecords
                .Include(m => m.Dirigent)
                .Include(m => m.Orchester)
                .Include(m => m.OrtEntity)
                .Include(m => m.Werke).ThenInclude(w => w.Komponist)
                .Include(m => m.Solisten)
                .FirstOrDefault(m => m.Id == id);
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
        // --- GENERISCHE METHODEN FÜR EINFACHE ENTITÄTEN ---
        // Holt alle Einträge einer beliebigen Klasse (z.B. Komponisten)
        //public async Task<List<T>> GetAllAsync<T>() where T : class
        //{
        //    return await _context.Set<T>().ToListAsync();
        //}

        // Findet einen Eintrag per ID
        public async Task<T?> GetByIdAsync<T>(int id) where T : class
        {
            return await _context.Set<T>().FindAsync(id);
        }

        // Speichert oder aktualisiert (EF erkennt anhand der ID meist selbst, ob Add oder Update)
        public async Task SaveAsync<T>(T entity) where T : class
        {
            // Check if entity is already tracked to avoid conflicts
            var idProp = typeof(T).GetProperty("Id");
            if (idProp != null && idProp.PropertyType == typeof(int))
            {
                int id = (int)idProp.GetValue(entity);
                if (id != 0)
                {
                    var existing = _context.ChangeTracker.Entries<T>()
                        .FirstOrDefault(e => (int)idProp.GetValue(e.Entity) == id);

                    if (existing != null)
                    {
                         existing.CurrentValues.SetValues(entity);
                         await _context.SaveChangesAsync();
                         return;
                    }
                }
            }

            _context.Update(entity); // Funktioniert für neue und bestehende Entities
            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync<T>(int id) where T : class
        {
            var entity = await _context.Set<T>().FindAsync(id);
            if (entity != null)
            {
                _context.Set<T>().Remove(entity);
                await _context.SaveChangesAsync();
            }
        }

        public List<Komponist> GetAllKomponisten() => _context.Komponisten.ToList();
        public List<Werk> GetAllWerke() => _context.Werke.Include(w => w.Komponist).ToList();
        public List<Orchester> GetAllOrchester() => _context.Orchester.ToList();
        public List<Dirigent> GetAllDirigenten() => _context.Dirigenten.ToList();
        public List<Solist> GetAllSolisten() => _context.Solisten.ToList();
        public List<Ort> GetAllOrte() => _context.Orte.ToList();

        // NoTracking variants for Master Data Management to avoid context tracking conflicts
        public List<Komponist> GetAllKomponistenNoTracking() => _context.Komponisten.AsNoTracking().ToList();
        public List<Werk> GetAllWerkeNoTracking() => _context.Werke.Include(w => w.Komponist).AsNoTracking().ToList();
        public List<Orchester> GetAllOrchesterNoTracking() => _context.Orchester.AsNoTracking().ToList();
        public List<Dirigent> GetAllDirigentenNoTracking() => _context.Dirigenten.AsNoTracking().ToList();
        public List<Solist> GetAllSolistenNoTracking() => _context.Solisten.AsNoTracking().ToList();
        public List<Ort> GetAllOrteNoTracking() => _context.Orte.AsNoTracking().ToList();

        public async Task AddKomponist(Komponist k)
        {
            _context.Komponisten.Add(k);
            await _context.SaveChangesAsync();
        }
        public async Task AddWerk(Werk w)
        {
            _context.Werke.Add(w);
            await _context.SaveChangesAsync();
        }
        public async Task AddOrchester(Orchester o)
        {
            _context.Orchester.Add(o);
            await _context.SaveChangesAsync();
        }
        public async Task AddDirigent(Dirigent d)
        {
            _context.Dirigenten.Add(d);
            await _context.SaveChangesAsync();
        }
        public async Task AddSolist(Solist s)
        {
            _context.Solisten.Add(s);
            await _context.SaveChangesAsync();
        }

        public async Task AddOrt(Ort o)
        {
            _context.Orte.Add(o);
            await _context.SaveChangesAsync();
        }

        public List<string> GetUsedKomponistenNames()
        {
            return _context.MusicRecords
                .SelectMany(m => m.Werke)
                .Select(w => w.Komponist)
                .Where(k => k != null)
                .Select(k => k.Name + (string.IsNullOrEmpty(k.Vorname) ? "" : ", " + k.Vorname))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public List<string> GetUsedWerkeNames()
        {
            return _context.MusicRecords
                .SelectMany(m => m.Werke)
                .Select(w => w.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public List<string> GetUsedOrchesterNames()
        {
            return _context.MusicRecords
                .Where(m => m.Orchester != null)
                .Select(m => m.Orchester.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public List<string> GetUsedDirigentenNames()
        {
            return _context.MusicRecords
                .Where(m => m.Dirigent != null)
                .Select(m => m.Dirigent.Name + (string.IsNullOrEmpty(m.Dirigent.Vorname) ? "" : ", " + m.Dirigent.Vorname))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public List<string> GetUsedSolistenNames()
        {
            return _context.MusicRecords
                .SelectMany(m => m.Solisten)
                .Select(s => s.Name + (string.IsNullOrEmpty(s.Vorname) ? "" : ", " + s.Vorname))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public List<string> GetUsedOrte()
        {
            return _context.Orte
                .Select(o => o.Name)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
        }

        public List<string> GetSpielSaisonList()
        {
            var list = _context.MusicRecords
                .Select(m => m.Spielsaison)
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            string currentSeason = $"{DateTime.UtcNow.Year}/{(DateTime.UtcNow.Year + 1) % 100}";
            if (!list.Contains(currentSeason))
            {
                list.Add(currentSeason);
                list.Sort();
            }
            return list;
        }

        public List<string> GetUsedSaisons()
        {
            return GetSpielSaisonList();
        }

        // --- SPEZIFISCHE LOGIK (Bleibt wie sie ist) ---

        //public async Task<string> SaveFile(int pid, string FileName, byte[] fileBytes, DocumentType type)
        //{
        //    // ... Deine existierende Dateilogik ...
        //}

    }
}
