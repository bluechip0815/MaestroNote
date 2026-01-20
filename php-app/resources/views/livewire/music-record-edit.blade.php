<div>
    <div class="card">
        <div class="card-header">
            {{ $record ? 'Edit Record' : 'Create Record' }}
        </div>
        <div class="card-body">

            @if(session()->has('message'))
                <div class="alert alert-success alert-dismissible fade show">
                    {{ session('message') }}
                    <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
                </div>
            @endif
            @if(session()->has('error'))
                <div class="alert alert-danger alert-dismissible fade show">
                    {{ session('error') }}
                    <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
                </div>
            @endif

            <form wire:submit="save">
                <div class="row mb-3">
                    <div class="col-md-6">
                        <label class="form-label">Datum</label>
                        <input type="date" wire:model="datum" class="form-control">
                        @error('datum') <span class="text-danger">{{ $message }}</span> @enderror
                    </div>
                    <div class="col-md-6">
                        <label class="form-label">Saison</label>
                        <input type="text" wire:model="spielsaison" class="form-control">
                        @error('spielsaison') <span class="text-danger">{{ $message }}</span> @enderror
                    </div>
                </div>

                <div class="row mb-3">
                    <div class="col-md-4">
                        <label class="form-label">Ort</label>
                        <select wire:model="ort_id" class="form-select">
                            <option value="">-- None --</option>
                            @foreach($orte as $ort)
                                <option value="{{ $ort->id }}">{{ $ort->name }}</option>
                            @endforeach
                        </select>
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Dirigent</label>
                        <select wire:model="dirigent_id" class="form-select">
                            <option value="">-- None --</option>
                            @foreach($dirigenten as $d)
                                <option value="{{ $d->id }}">{{ $d->name }} {{ $d->vorname }}</option>
                            @endforeach
                        </select>
                    </div>
                    <div class="col-md-4">
                        <label class="form-label">Orchester</label>
                        <select wire:model="orchester_id" class="form-select">
                            <option value="">-- None --</option>
                            @foreach($orchester as $o)
                                <option value="{{ $o->id }}">{{ $o->name }}</option>
                            @endforeach
                        </select>
                    </div>
                </div>

                <div class="mb-3">
                    <label class="form-label">Werke</label>
                    <select wire:model="selectedWerke" class="form-select" multiple size="8">
                        @foreach($allWerke as $werk)
                            <option value="{{ $werk->id }}">
                                {{ $werk->komponist ? $werk->komponist->name : 'Unknown' }}: {{ $werk->name }}
                            </option>
                        @endforeach
                    </select>
                    <small class="text-muted">Hold Ctrl/Cmd to select multiple.</small>
                </div>

                <div class="mb-3">
                    <label class="form-label">Solisten</label>
                    <select wire:model="selectedSolisten" class="form-select" multiple size="5">
                        @foreach($allSolisten as $s)
                            <option value="{{ $s->id }}">{{ $s->name }} {{ $s->vorname }}</option>
                        @endforeach
                    </select>
                </div>

                <div class="mb-3">
                    <div class="d-flex justify-content-between align-items-center">
                        <label class="form-label">Bewertung / Note</label>
                        <button type="button" wire:click="autoFillAi" class="btn btn-sm btn-outline-info" title="Generate review from selected works">
                             Auto-fill with AI
                        </button>
                    </div>
                    <textarea wire:model="bewertung" class="form-control" rows="4"></textarea>
                    <div wire:loading wire:target="autoFillAi" class="text-info small mt-1">
                        Generating content...
                    </div>
                </div>

                <div class="mb-4">
                    <label class="form-label">Documents (PDF / Images)</label>

                    @if($record && $record->documents->isNotEmpty())
                        <ul class="list-group mb-2">
                            @foreach($record->documents as $doc)
                                <li class="list-group-item d-flex justify-content-between align-items-center">
                                    <span>
                                        @if($doc->document_type === \App\Enums\DocumentType::Pdf)
                                            <span class="badge bg-danger">PDF</span>
                                        @else
                                            <span class="badge bg-primary">IMG</span>
                                        @endif
                                        {{ $doc->file_name }}
                                    </span>
                                    <button type="button" wire:click="deleteDocument({{ $doc->id }})" class="btn btn-sm btn-outline-danger">
                                        Delete
                                    </button>
                                </li>
                            @endforeach
                        </ul>
                    @endif

                    <input type="file" wire:model="newFiles" class="form-control" multiple>
                    <div wire:loading wire:target="newFiles" class="text-muted small">Uploading...</div>
                    @error('newFiles.*') <span class="text-danger">{{ $message }}</span> @enderror
                </div>

                <div class="d-flex justify-content-between">
                    <a href="/" class="btn btn-secondary">Back</a>
                    <button type="submit" class="btn btn-primary">Save Record</button>
                </div>
            </form>
        </div>
    </div>
</div>
