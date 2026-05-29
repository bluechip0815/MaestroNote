# Timeout-Analyse: OpenAI API mit gpt-5.2-pro

## 1. Ursache des Fehlers
Der Fehler `TaskCanceledException: The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.` tritt auf, weil der Standard-Timeout eines `HttpClient` in .NET exakt 100 Sekunden beträgt. Das Modell `gpt-5.2-pro` braucht für komplexe oder lange Responses (oft bei Structured Outputs/JSON) teilweise länger als diese 100 Sekunden, um die vollständige Antwort zu generieren. Nach 100 Sekunden bricht der `HttpClient` die Verbindung hart ab, was zu der beobachteten `SocketException (995)` und `IOException` führt.

## 2. Warum kein Modell-/API-Fehler vorliegt
Der Fehler wird clientseitig vom .NET `HttpClient` ausgelöst, nicht vom OpenAI-Server. Wäre es ein Server-Fehler, würden wir einen HTTP-Statuscode (z.B. 500, 502, 504) oder zumindest einen generischen Abbruch ohne die spezifische Meldung "configured HttpClient.Timeout" sehen. Die exakte Dauer von 100 Sekunden ist das unmissverständliche Symptom des .NET Defaults.

## 3. Die Rolle von HttpClient.Timeout
Die Eigenschaft `HttpClient.Timeout` bestimmt die maximale Zeit, die ein Request (vom Senden des Headers bis zum Empfangen des letzten Bytes des Bodys) dauern darf. Wenn `await HttpClient.SendAsync()` aufgerufen wird, fängt die Uhr an zu ticken.

## 4. Socket Exhaustion & HttpClient Lifetime
Die aktuelle Implementierung nutzt eine statische `HttpClientFactory`, die bei jedem Request einen `new HttpClient()` mit zugehörigem `HttpClientHandler` erzeugt. Dies ist ein bekanntes Anti-Pattern in .NET. Obwohl die Objekte garbage-collected werden, bleiben die zugrunde liegenden TCP-Sockets im Status `TIME_WAIT` (typischerweise für bis zu 4 Minuten) auf dem Betriebssystem bestehen. Bei vielen Requests führt das unweigerlich zu **Socket Exhaustion**.

**Lösung:** Die integrierte `IHttpClientFactory` von ASP.NET Core verwenden.

## 5. Streaming & Lange Responses
Lange JSON-Responses führen bei großen Prompts oft zu langen Generierungszeiten.
* **Streaming (`stream: true`):** Würde bedeuten, dass die Antwort Token für Token kommt. Das verhindert zwar einfache Timeouts, ist aber bei JSON Schema/Structured Outputs extrem komplex zu parsen (da das JSON im Stream oft noch unvollständig/ungültig ist).
* **`HttpCompletionOption.ResponseHeadersRead`:** Eine deutlich einfachere Zwischenlösung. Normalerweise wartet `SendAsync`, bis der *gesamte* Body im Speicher ist. Mit `ResponseHeadersRead` kehrt die Methode zurück, sobald die Header da sind. Das puffert den Speicher besser und erlaubt dem HttpClient flexibler mit dem Stream umzugehen.

## 6. Lösungsstrategie & Empfehlungen
1. **HttpClientFactory:** Vollständiger Wechsel auf `Microsoft.Extensions.Http` (`IHttpClientFactory`). Konfiguration eines benannten Clients in der `Program.cs`.
2. **Timeout:** Setzen des `HttpClient.Timeout` auf `Timeout.InfiniteTimeSpan`.
3. **CancellationToken:** Einführung expliziter Timeouts über eine `CancellationTokenSource` (z. B. 10 Minuten), die von der Blazor-Komponente bis in den `OpenAiProvider` durchgereicht wird. Das erlaubt es auch, UI-Abbrüche sauber abzufangen.
4. **Logging:** Hinzufügen von Timings (`Stopwatch`), um zu sehen, ob der Abbruch durch den Token oder einen echten Socket-Fehler kam.

### Beispielcode-Architektur
```csharp
// Program.cs
builder.Services.AddHttpClient("AiClient", client => {
    client.Timeout = Timeout.InfiniteTimeSpan;
});

// Provider
var request = new HttpRequestMessage(HttpMethod.Post, url);
var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
```
