<div>
    <div class="card mb-4">
        <div class="card-body">
            <div class="row g-3">
                <div class="col-md-3">
                    <select wire:model.live="category" class="form-select">
                        <option value="">Filter Category...</option>
                        <option value="Werk">Werk</option>
                        <option value="Komponist">Komponist</option>
                        <option value="Dirigent">Dirigent</option>
                        <option value="Orchester">Orchester</option>
                        <option value="Solist">Solist</option>
                        <option value="Ort">Ort</option>
                        <option value="Datum">Datum</option>
                        <option value="Saisson">Saison</option>
                        <option value="Note">Note (Bewertung)</option>
                    </select>
                </div>

                <div class="col-md-6">
                    @if($category === 'Datum')
                        <div class="input-group">
                            <span class="input-group-text">From</span>
                            <input type="date" wire:model.live="dateFrom" class="form-control">
                            <span class="input-group-text">To</span>
                            <input type="date" wire:model.live="dateTo" class="form-control">
                        </div>
                    @elseif(!empty($dropdownData))
                        <select wire:model.live="searchTerm" class="form-select">
                            <option value="">Select...</option>
                            @foreach($dropdownData as $item)
                                <option value="{{ $item }}">{{ $item }}</option>
                            @endforeach
                        </select>
                    @else
                        <input type="text" wire:model.live.debounce.300ms="searchTerm" class="form-control" placeholder="Search...">
                    @endif
                </div>

                <div class="col-md-3">
                    <a href="/create" class="btn btn-primary w-100">Add New Record</a>
                </div>
            </div>
        </div>
    </div>

    @if(session()->has('message'))
        <div class="alert alert-success alert-dismissible fade show" role="alert">
            {{ session('message') }}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    @endif

    <div class="table-responsive">
        <table class="table modern-table">
            <thead>
                <tr>
                    <th>Date</th>
                    <th>Works / Composers</th>
                    <th>Details (Orchestra, Conductor, Solists)</th>
                    <th>Location / Rating</th>
                    <th>Actions</th>
                </tr>
            </thead>
            <tbody>
                @foreach($records as $record)
                    <tr>
                        <td style="min-width: 100px;">
                            <div class="fw-bold">{{ $record->datum->format('d.m.Y') }}</div>
                            <small class="text-muted">{{ $record->spielsaison }}</small>
                        </td>
                        <td>
                            <div>
                                <strong>Works:</strong>
                                {{ $record->werke->pluck('name')->join(', ') }}
                            </div>
                            <div class="mt-1">
                                <small class="text-muted">
                                    <strong>Composers:</strong>
                                    {{ $record->werke->map(fn($w) => optional($w->komponist)->name)->filter()->unique()->join(', ') }}
                                </small>
                            </div>
                        </td>
                        <td>
                            @if($record->orchester)
                                <div><i class="bi bi-people"></i> {{ $record->orchester->name }}</div>
                            @endif
                            @if($record->dirigent)
                                <div><i class="bi bi-person-badge"></i> {{ $record->dirigent->name }}</div>
                            @endif
                            @if($record->solisten->isNotEmpty())
                                <div class="small text-muted">
                                    <i class="bi bi-person"></i>
                                    {{ $record->solisten->pluck('name')->join(', ') }}
                                </div>
                            @endif
                        </td>
                        <td>
                            <div>{{ optional($record->ort)->name }}</div>
                            @if($record->bewertung)
                                <div class="mt-1 small fst-italic text-muted">"{{ Str::limit($record->bewertung, 50) }}"</div>
                            @endif
                        </td>
                        <td>
                            <div class="dropdown">
                                <button class="btn btn-sm btn-outline-secondary dropdown-toggle" type="button" data-bs-toggle="dropdown">
                                    Actions
                                </button>
                                <ul class="dropdown-menu">
                                    <li><a class="dropdown-item" href="/edit/{{ $record->id }}">Edit</a></li>
                                    <li><a class="dropdown-item" href="#" wire:click.prevent="delete({{ $record->id }})" onclick="confirm('Are you sure?') || event.stopImmediatePropagation()">Delete</a></li>
                                </ul>
                            </div>
                        </td>
                    </tr>
                @endforeach
            </tbody>
        </table>
    </div>

    <div class="mt-4">
        {{ $records->links() }}
    </div>
</div>
