<?php

namespace App\Livewire;

use App\Services\MusicService;
use Livewire\Component;
use Livewire\WithPagination;

class MusicRecordIndex extends Component
{
    use WithPagination;

    public $category = '';
    public $searchTerm = '';
    public $dateFrom = '';
    public $dateTo = '';
    public $limit = 10;

    protected $paginationTheme = 'bootstrap';

    public function updatedCategory()
    {
        $this->resetPage();
        $this->searchTerm = ''; // Reset search term when category changes?
        // C# doesn't explicitly reset, but usually UI does.
    }

    public function updatedSearchTerm()
    {
        $this->resetPage();
    }

    public function delete(int $id, MusicService $service)
    {
        $service->deleteRecord($id);
        session()->flash('message', 'Record deleted successfully.');
    }

    public function render(MusicService $service)
    {
        $records = $service->getDisplayRecords(
            $this->category ?: null,
            $this->searchTerm ?: null,
            $this->dateFrom ?: null,
            $this->dateTo ?: null,
            $this->limit
        );

        // Fetch dropdown data based on category
        $dropdownData = [];
        if ($this->category) {
            switch ($this->category) {
                case 'Komponist':
                    $dropdownData = $service->getUsedKomponistenNames();
                    break;
                case 'Werk':
                    $dropdownData = $service->getUsedWerkeNames();
                    break;
                case 'Orchester':
                    $dropdownData = $service->getUsedOrchesterNames();
                    break;
                case 'Dirigent':
                    $dropdownData = $service->getUsedDirigentenNames();
                    break;
                case 'Ort':
                    $dropdownData = $service->getUsedOrte();
                    break;
                case 'Saisson':
                    $dropdownData = $service->getSpielSaisonList();
                    break;
            }
        }

        return view('livewire.music-record-index', [
            'records' => $records,
            'dropdownData' => $dropdownData
        ]);
    }
}
