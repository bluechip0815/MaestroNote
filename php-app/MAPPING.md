# Mapping C# to PHP

| Feature / Logic | C# Component | PHP Component | Notes |
| :--- | :--- | :--- | :--- |
| **Framework** | .NET 8 / Blazor Server | Laravel 11 / Livewire 3 | Similar Component-based architecture. |
| **Database** | Entity Framework Core (MySQL) | Eloquent ORM (MySQL) | 1:1 Schema Mapping. |
| **Routing** | `App.razor`, `@page` directives | `routes/web.php` | Centralized routing in PHP. |
| **Dependency Injection** | `Program.cs` (`AddScoped`) | Laravel Service Container | Automatic injection in Controllers/Livewire. |
| **Logging** | Serilog | Monolog (Laravel Log) | Uses standard `Log::info()` facade. |
| **AI Integration** | `AiService.cs`, `HttpClientFactory` | `App\Services\AiService.php` | Uses `openai-php/client` library. |

## Data Models

| C# Class | PHP Model | Table |
| :--- | :--- | :--- |
| `MusicRecord` | `App\Models\MusicRecord` | `music_records` |
| `Komponist` | `App\Models\Komponist` | `komponisten` |
| `Werk` | `App\Models\Werk` | `werke` |
| `Orchester` | `App\Models\Orchester` | `orchester` |
| `Dirigent` | `App\Models\Dirigent` | `dirigenten` |
| `Solist` | `App\Models\Solist` | `solisten` |
| `Ort` | `App\Models\Ort` | `orte` |
| `Document` | `App\Models\Document` | `documents` |

## Key Logic

| Functionality | C# Method | PHP Method |
| :--- | :--- | :--- |
| **Filtering** | `MusicService.GetDisplayRecords` | `App\Services\MusicService::getDisplayRecords` |
| **Dropdowns** | `MusicService.GetUsed...` | `App\Services\MusicService::getUsed...` |
| **Editing** | `Edit.razor` | `App\Livewire\MusicRecordEdit` |
| **File Upload** | `MusicService.SaveFile` | Livewire `WithFileUploads` (Native) |
| **Search** | `Index.razor` (Input binding) | Livewire `MusicRecordIndex` (`$searchTerm`) |

## Security Implementation

1.  **CSRF**: Laravel automatically includes CSRF tokens in all forms (`@csrf`) and Livewire handles it internally for components.
2.  **SQL Injection**: All queries use Eloquent ORM or PDO bindings (in `whereRaw`).
3.  **XSS**: Blade templates (`{{ $var }}`) automatically escape output.
4.  **Authentication**: (Recommended) Use Basic Auth via Web Server (`.htaccess`) as requested, or Laravel Sanctum/Breeze.

## Deviations

*   **Modals for Creation**: The C# app allows creating new Master Data (e.g., new Komponist) via a popup *inside* the Edit form. The current PHP port focuses on the core Record editing. New Master Data should be added via separate flows or by enhancing `MusicRecordEdit` with dynamic modal events in a future iteration.
*   **Access Levels**: C# code had `PasswordRO`/`PasswordRW`. The PHP port relies on the Web Server or Laravel's Auth system for access control.
