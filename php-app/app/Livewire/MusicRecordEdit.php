<?php

namespace App\Livewire;

use App\Models\MusicRecord;
use App\Models\Dirigent;
use App\Models\Orchester;
use App\Models\Ort;
use App\Models\Werk;
use App\Models\Solist;
use App\Models\Document;
use App\Enums\DocumentType;
use App\Services\AiService;
use Livewire\Component;
use Livewire\WithFileUploads;
use Illuminate\Support\Str;

class MusicRecordEdit extends Component
{
    use WithFileUploads;

    public ?MusicRecord $record = null;

    // Form Fields
    public $datum;
    public $spielsaison;
    public $bewertung;
    public $bezeichnung;

    public $dirigent_id;
    public $orchester_id;
    public $ort_id;

    public $selectedWerke = [];
    public $selectedSolisten = [];

    // Uploads
    public $newFiles = [];

    protected function rules()
    {
        return [
            'datum' => 'required|date',
            'spielsaison' => 'required|string|max:64',
            'bewertung' => 'nullable|string',
            'dirigent_id' => 'nullable|exists:dirigenten,id',
            'orchester_id' => 'nullable|exists:orchester,id',
            'ort_id' => 'nullable|exists:orte,id',
            'selectedWerke' => 'array',
            'selectedSolisten' => 'array',
            'newFiles.*' => 'nullable|file|max:10240', // 10MB max
        ];
    }

    public function mount($id = null)
    {
        if ($id) {
            $this->record = MusicRecord::with(['werke', 'solisten', 'documents'])->findOrFail($id);

            $this->datum = $this->record->datum->format('Y-m-d');
            $this->spielsaison = $this->record->spielsaison;
            $this->bewertung = $this->record->bewertung;
            $this->bezeichnung = $this->record->bezeichnung;

            $this->dirigent_id = $this->record->dirigent_id;
            $this->orchester_id = $this->record->orchester_id;
            $this->ort_id = $this->record->ort_id;

            $this->selectedWerke = $this->record->werke->pluck('id')->toArray();
            $this->selectedSolisten = $this->record->solisten->pluck('id')->toArray();
        } else {
            $this->datum = date('Y-m-d');
            $this->spielsaison = date('Y') . '/' . (date('y') + 1);
        }
    }

    public function autoFillAi(AiService $aiService)
    {
        // Example AI usage: Suggest a summary or rating based on the Works
        // In a real scenario, this would send a prompt with the selected Werke/Komponisten.

        $works = Werk::whereIn('id', $this->selectedWerke)->with('komponist')->get();
        $workNames = $works->map(fn($w) => ($w->komponist->name ?? '') . ': ' . $w->name)->join(', ');

        if (empty($workNames)) {
            session()->flash('error', 'Please select some works first.');
            return;
        }

        $prompt = "Write a short German review/summary for a concert featuring: " . $workNames;
        $suggestion = $aiService->requestAiData($prompt);

        if ($suggestion) {
            $this->bewertung = $suggestion;
            session()->flash('message', 'AI Suggestion generated.');
        } else {
            session()->flash('error', 'AI Service unreachable.');
        }
    }

    public function save()
    {
        $this->validate();

        if ($this->record) {
            $this->record->update([
                'datum' => $this->datum,
                'spielsaison' => $this->spielsaison,
                'bewertung' => $this->bewertung,
                'bezeichnung' => $this->bezeichnung ?? '',
                'dirigent_id' => $this->dirigent_id ?: null,
                'orchester_id' => $this->orchester_id ?: null,
                'ort_id' => $this->ort_id ?: null,
            ]);
        } else {
            $this->record = MusicRecord::create([
                'datum' => $this->datum,
                'spielsaison' => $this->spielsaison,
                'bewertung' => $this->bewertung,
                'bezeichnung' => $this->bezeichnung ?? '',
                'dirigent_id' => $this->dirigent_id ?: null,
                'orchester_id' => $this->orchester_id ?: null,
                'ort_id' => $this->ort_id ?: null,
            ]);
        }

        // Sync relationships
        $this->record->werke()->sync($this->selectedWerke);
        $this->record->solisten()->sync($this->selectedSolisten);

        // Handle File Uploads
        foreach ($this->newFiles as $file) {
            $filename = $file->getClientOriginalName();
            $encryptedName = $file->hashName(); // Generates random name
            $extension = $file->getClientOriginalExtension();

            // Determine type
            $type = in_array(strtolower($extension), ['jpg','jpeg','png','webp'])
                ? DocumentType::Image
                : DocumentType::Pdf;

            // Store file
            $file->storeAs('documents', $encryptedName, 'public');

            // Save to DB
            $this->record->documents()->create([
                'file_name' => $filename,
                'encrypted_name' => $encryptedName,
                'document_type' => $type,
            ]);
        }

        // Clear files after save
        $this->newFiles = [];

        session()->flash('message', 'Record saved successfully.');

        // Reload record to show new documents
        $this->record->refresh();
    }

    public function deleteDocument($docId)
    {
        $doc = Document::find($docId);
        if ($doc && $doc->music_record_id === $this->record->id) {
            // Delete file from storage (mocked here, in real Laravel: Storage::disk('public')->delete('documents/'.$doc->encrypted_name))
            // We'll just delete the DB record.
            $doc->delete();
            $this->record->refresh();
        }
    }

    public function render()
    {
        return view('livewire.music-record-edit', [
            'dirigenten' => Dirigent::orderBy('name')->get(),
            'orchester' => Orchester::orderBy('name')->get(),
            'orte' => Ort::orderBy('name')->get(),
            'allWerke' => Werk::with('komponist')->orderBy('name')->get(),
            'allSolisten' => Solist::orderBy('name')->get(),
        ]);
    }
}
