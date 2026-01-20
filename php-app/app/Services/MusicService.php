<?php

namespace App\Services;

use App\Models\MusicRecord;
use App\Models\Komponist;
use App\Models\Werk;
use App\Models\Orchester;
use App\Models\Dirigent;
use App\Models\Solist;
use App\Models\Ort;
use Illuminate\Database\Eloquent\Builder;
use Illuminate\Pagination\LengthAwarePaginator;
use Illuminate\Support\Facades\DB;

class MusicService
{
    public function getDisplayRecords(
        ?string $category = null,
        ?string $searchTerm = null,
        ?string $dateFrom = null,
        ?string $dateTo = null,
        int $limit = 10
    ): LengthAwarePaginator {
        $query = MusicRecord::with([
            'dirigent',
            'orchester',
            'ort',
            'werke.komponist',
            'solisten'
        ]);

        if ($category && $searchTerm) {
            $this->applyCategoryFilter($query, $category, $searchTerm);
        }

        // Date Logic override if Category is 'Datum' (handled in applyCategoryFilter, but logic in C# is separate block under switch)
        // Wait, C# switch case "Datum" does date logic.

        // Re-reading C#: "Datum" is a category.
        // But the method signature has dateFrom/dateTo.
        // Logic: if (category == "Datum") apply date filter.

        if ($category === 'Datum') {
             if ($dateFrom) {
                $query->whereDate('datum', '>=', $dateFrom);
             }
             if ($dateTo) {
                $query->whereDate('datum', '<=', $dateTo);
             }
        }

        $query->orderByDesc('datum');

        return $query->paginate($limit);
    }

    protected function applyCategoryFilter(Builder $query, string $category, string $searchTerm): void
    {
        switch ($category) {
            case 'Werk':
                $query->whereHas('werke', function ($q) use ($searchTerm) {
                    $q->where('name', 'like', "%{$searchTerm}%");
                });
                break;

            case 'Komponist':
                // Search in Werke -> Komponist
                $query->whereHas('werke.komponist', function ($q) use ($searchTerm) {
                    $q->whereRaw("CONCAT(name, IF(vorname != '', CONCAT(', ', vorname), '')) LIKE ?", ["%{$searchTerm}%"]);
                });
                break;

            case 'Dirigent':
                $query->whereHas('dirigent', function ($q) use ($searchTerm) {
                    $q->whereRaw("CONCAT(name, IF(vorname != '', CONCAT(', ', vorname), '')) LIKE ?", ["%{$searchTerm}%"]);
                });
                break;

            case 'Solist':
                $query->whereHas('solisten', function ($q) use ($searchTerm) {
                    $q->whereRaw("CONCAT(name, IF(vorname != '', CONCAT(', ', vorname), '')) LIKE ?", ["%{$searchTerm}%"]);
                });
                break;

            case 'Orchester':
                $query->whereHas('orchester', function ($q) use ($searchTerm) {
                    $q->where('name', 'like', "%{$searchTerm}%");
                });
                break;

            case 'Ort':
                $query->whereHas('ort', function ($q) use ($searchTerm) {
                    $q->where('name', 'like', "%{$searchTerm}%");
                });
                break;

            case 'Saisson':
                $query->where('spielsaison', $searchTerm);
                break;

            case 'Note':
                // Supports * and ? wildcards
                $pattern = str_replace(['*', '?'], ['%', '_'], $searchTerm);
                $query->where('bewertung', 'like', $pattern);
                break;
        }
    }

    // --- Dropdown Helpers ---

    public function getUsedKomponistenNames(): array
    {
        // Complex because we need distinct names from USED records only
        // But C# uses: _context.MusicRecords.SelectMany(m => m.Werke)...
        // This effectively gets ALL Komponisten used in music records.

        return Komponist::whereHas('werke.musicRecords')
            ->get()
            ->map(fn($k) => $k->name . ($k->vorname ? ", {$k->vorname}" : ""))
            ->unique()
            ->sort()
            ->values()
            ->toArray();
    }

    public function getUsedWerkeNames(): array
    {
        return Werk::whereHas('musicRecords')
            ->pluck('name')
            ->unique()
            ->sort()
            ->values()
            ->toArray();
    }

    public function getUsedOrchesterNames(): array
    {
        return Orchester::whereHas('musicRecords') // implicit since Orchester is parent? No, Orchester hasMany MusicRecord usually.
            // Wait, MusicRecord belongsTo Orchester.
            // So we want Orchester that are referenced by at least one MusicRecord.
            // Orchester doesn't have musicRecords relationship defined in my Model yet?
            // I defined belongsTo in MusicRecord.
            // I should define hasMany in Orchester if I want whereHas.
            // Or simply query MusicRecord distinct orchester_id.

           // Let's do it via MusicRecord query to be safe and match C# logic
           MusicRecord::with('orchester')
               ->whereNotNull('orchester_id')
               ->get()
               ->pluck('orchester.name')
               ->unique()
               ->sort()
               ->values()
               ->toArray();
    }

    // Actually, distinct query on DB is more efficient.
    public function getUsedOrte(): array
    {
        return Ort::whereHas('musicRecords') // Need relationship in Ort model?
            // In C# it selects from Orte table directly?
            // "return _context.Orte.Select(o => o.Name)..." -> Yes.
            // Wait, C# getUsedKomponistenNames used SelectMany from MusicRecords.
            // But GetUsedOrte uses _context.Orte.

            Ort::orderBy('name')->pluck('name')->unique()->toArray();
    }

    public function getSpielSaisonList(): array
    {
        $seasons = MusicRecord::select('spielsaison')
            ->distinct()
            ->whereNotNull('spielsaison')
            ->where('spielsaison', '!=', '')
            ->orderBy('spielsaison')
            ->pluck('spielsaison')
            ->toArray();

        $currentSeason = date('Y') . '/' . (date('y') + 1);
        if (!in_array($currentSeason, $seasons)) {
            $seasons[] = $currentSeason;
            sort($seasons);
        }

        return $seasons;
    }

    // CRUD wrappers if needed, or use Eloquent directly in Controller/Livewire
    public function createRecord(array $data): MusicRecord
    {
        return MusicRecord::create($data);
    }

    public function updateRecord(MusicRecord $record, array $data): bool
    {
        return $record->update($data);
    }

    public function deleteRecord(int $id): void
    {
        MusicRecord::destroy($id);
    }
}
