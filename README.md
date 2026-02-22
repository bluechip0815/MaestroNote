# MaestroNotes

MaestroNotes ist eine Webanwendung zur Verwaltung von persönlichen Musiknotizen, Konzertbesuchen und Rezensionen. Sie ermöglicht es, detaillierte Informationen über aufgeführte Werke, Komponisten, Dirigenten, Solisten, Orchester und Veranstaltungsorte zu speichern und zu organisieren.

## Funktionen und Features

Die Anwendung bietet umfassende Funktionen zur Dokumentation und Verwaltung musikalischer Erlebnisse:

*   **Verwaltung von Musikdatensätzen (MusicRecords)**:
    *   Erfassen von Konzertbesuchen mit Datum, Spielsaison, Ort und Bewertung.
    *   Verknüpfung mit Dirigenten, Orchestern, Werken und Solisten.
    *   Hinzufügen einer individuellen Bezeichnung und Bewertung (Rezension).
    *   **Volltextsuche**: Filtern nach Kategorie (Werk, Komponist, Dirigent, etc.), Datum oder Freitext.

*   **Stammdatenverwaltung**:
    *   **Komponisten**: Name, Vorname, Geburts- und Sterbedatum, Notizen.
    *   **Werke**: Titel, Verknüpfung zum Komponisten, Notizen.
    *   **Dirigenten & Solisten**: Name, Vorname, Lebensdaten, Notizen.
    *   **Orchester**: Name, Gründungsdatum, Notizen.
    *   **Orte**: Name, Notizen.

*   **Dokumentenmanagement**:
    *   Upload von PDF-Dokumenten (z.B. Programmhefte) und Bildern zu jedem Datensatz.
    *   Vorschau und Download der Dateien.
    *   Vormerken-Funktion für den Export.

*   **KI-Unterstützung**:
    *   Automatische Vervollständigung von fehlenden Informationen (z.B. Lebensdaten, Werkbeschreibungen) durch Integration von KI-Diensten (ChatGPT, Gemini, Anthropic).
    *   Einstellbar über `appsettings.json`.

*   **Export**:
    *   Export der gesammelten Daten als RTF-Dokument (inkl. Bilder).
    *   Versand als ZIP-Archiv per E-Mail an den angemeldeten Benutzer.

*   **Benutzerverwaltung & Sicherheit**:
    *   Rollenbasiertes System (Admin, Viewer).
    *   Passwortloses Login-Verfahren ("Magic Link").

## Datenbank Setup

Die Anwendung verwendet eine MySQL-Datenbank (Version 8.0 oder neuer). Nachfolgend finden Sie die SQL-Statements zum Erstellen der Tabellenstruktur.

```sql
-- Tabelle: Users
-- UserLevel: 0 = Viewer, 1 = Admin
CREATE TABLE Users (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(12) NOT NULL,
    Email VARCHAR(60) NOT NULL,
    UserLevel INT NOT NULL DEFAULT 0,
    UNIQUE (Name),
    UNIQUE (Email)
);

-- Tabelle: LoginTokens
CREATE TABLE LoginTokens (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Token CHAR(36) NOT NULL,
    UserName VARCHAR(12) NOT NULL,
    CreatedAt DATETIME NOT NULL,
    IsUsed TINYINT(1) NOT NULL DEFAULT 0
);

-- Tabelle: Orte
CREATE TABLE Orte (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    Note VARCHAR(2000)
);

-- Tabelle: Orchester
CREATE TABLE Orchester (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(100) NOT NULL,
    Founded DATETIME NULL,
    Note VARCHAR(1000)
);

-- Tabelle: Komponisten
CREATE TABLE Komponisten (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Vorname VARCHAR(50),
    Name VARCHAR(50),
    Born DATETIME NULL,
    Died DATETIME NULL,
    Note VARCHAR(1000)
);

-- Tabelle: Dirigenten
CREATE TABLE Dirigenten (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Vorname VARCHAR(50),
    Name VARCHAR(50),
    Born DATETIME NULL,
    Died DATETIME NULL,
    Note VARCHAR(1000)
);

-- Tabelle: Solisten
CREATE TABLE Solisten (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Vorname VARCHAR(50),
    Name VARCHAR(50),
    Born DATETIME NULL,
    Died DATETIME NULL,
    Note VARCHAR(1000)
);

-- Tabelle: Werke
CREATE TABLE Werke (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(200) NOT NULL,
    Note VARCHAR(1000),
    KomponistId INT,
    FOREIGN KEY (KomponistId) REFERENCES Komponisten(Id) ON DELETE SET NULL
);

-- Tabelle: MusicRecords (Haupttabelle)
CREATE TABLE MusicRecords (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Bezeichnung VARCHAR(200),
    Datum DATETIME NOT NULL,
    Spielsaison VARCHAR(64),
    Bewertung VARCHAR(2000),
    DirigentId INT,
    OrchesterId INT,
    OrtId INT,
    FOREIGN KEY (DirigentId) REFERENCES Dirigenten(Id) ON DELETE SET NULL,
    FOREIGN KEY (OrchesterId) REFERENCES Orchester(Id) ON DELETE SET NULL,
    FOREIGN KEY (OrtId) REFERENCES Orte(Id) ON DELETE SET NULL
);

-- Tabelle: Documents
-- DocumentType: 0 = Pdf, 1 = Image
CREATE TABLE Documents (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    FileName VARCHAR(250),
    EncryptedName VARCHAR(250),
    DocumentType INT NOT NULL,
    MusicRecordId INT NOT NULL,
    Vormerken TINYINT(1) NOT NULL DEFAULT 0,
    FOREIGN KEY (MusicRecordId) REFERENCES MusicRecords(Id) ON DELETE CASCADE
);

-- Verknüpfungstabelle: MusicRecords <-> Werke
CREATE TABLE MusicRecordWerk (
    MusicRecordsId INT NOT NULL,
    WerkeId INT NOT NULL,
    PRIMARY KEY (MusicRecordsId, WerkeId),
    FOREIGN KEY (MusicRecordsId) REFERENCES MusicRecords(Id) ON DELETE CASCADE,
    FOREIGN KEY (WerkeId) REFERENCES Werke(Id) ON DELETE CASCADE
);

-- Verknüpfungstabelle: MusicRecords <-> Solisten
CREATE TABLE MusicRecordSolist (
    MusicRecordsId INT NOT NULL,
    SolistenId INT NOT NULL,
    PRIMARY KEY (MusicRecordsId, SolistenId),
    FOREIGN KEY (MusicRecordsId) REFERENCES MusicRecords(Id) ON DELETE CASCADE,
    FOREIGN KEY (SolistenId) REFERENCES Solisten(Id) ON DELETE CASCADE
);
```

### Initialer Benutzer
Beim ersten Start der Anwendung wird, falls noch keine Benutzer existieren, automatisch ein Admin-Benutzer angelegt:
*   **Name**: Admin
*   **E-Mail**: admin@example.com

## Login Prozess

MaestroNotes verwendet ein sicheres, passwortloses Login-Verfahren ("Magic Link"), das auf E-Mail-Verifizierung basiert.

1.  **Login-Anfrage**:
    *   Der Benutzer gibt auf der Login-Seite (`/login`) seinen Benutzernamen ein.
    *   Das System prüft, ob ein Benutzer mit diesem Namen existiert.

2.  **Token-Generierung & E-Mail-Versand**:
    *   Ein eindeutiges Token (GUID) wird generiert und mit einem Zeitstempel in der Tabelle `LoginTokens` gespeichert.
    *   Eine E-Mail wird an die hinterlegte Adresse des Benutzers gesendet. Diese E-Mail enthält einen Link zur Verifizierung (z.B. `https://maestronotes.local/auth/verify?token=...`).

3.  **Verifizierung**:
    *   Der Benutzer klickt auf den Link in der E-Mail.
    *   Der `AuthController` prüft das übergebene Token auf Gültigkeit:
        *   Das Token muss in der Datenbank existieren.
        *   Das Token darf noch nicht verwendet worden sein (`IsUsed = 0`).
        *   Das Token darf nicht älter als 30 Tage sein.

4.  **Authentifizierung**:
    *   Bei erfolgreicher Prüfung wird das Token als "verwendet" markiert (`IsUsed = 1`).
    *   Ein sicheres Authentifizierungs-Cookie (`MaestroNotesAuth`) wird gesetzt, das den Benutzer für 30 Tage eingeloggt lässt.
