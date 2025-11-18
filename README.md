# SaraSenteApp
Konsolowa aplikacja .NET 8 do generowania i aktualizacji skryptów metadanych z bazy Firebird 5.0.  
Pozwala na:
- budowę nowej bazy danych na podstawie skryptów,
- eksport istniejącej bazy do plików SQL,
- aktualizację bazy na podstawie skryptów.

## Wymagania
- .NET 8 SDK
- Firebird 5.0 client (fbclient.dll)
  
## Uruchomienie
Aplikacja przyjmuje trzy główne polecenia:

### 1. Budowa bazy danych ze skryptów
Tworzy nową bazę w podanym katalogu i wykonuje skrypty z wybranego katalogu.
Nazwa nowej bazy to NEW_DATABASE.FDB.
Dane logowania ustawione są zgodnie z domyślną konfiguracją Firebirda.
np. dotnet run -- build-db --db-dir "C:\ShopFB" --scripts-dir "C:\ExportScripts"

### 2. Eksport skryptów z istniejącej bazy
Generuje pliki SQL z domen, tabel i procedur.
np. dotnet run -- export-scripts --connection-string "User=SYSDBA;Password=masterkey;Database=C:\ShopFB\Shop.fdb;DataSource=localhost;Port=3050;Dialect=3;" --output-dir "C:\ExportScripts"

### 3. Aktualizacja istniejącej bazy na podstawie skryptów
Wykonuje skrypty z katalogu na istniejącej bazie.
np. dotnet run -- update-db --connection-string "User=SYSDBA;Password=masterkey;Database=C:\ShopFB\NEW_DATABASE.FDB;DataSource=localhost;Port=3050;Dialect=3;" --scripts-dir "C:\ExportScripts"

### Uwagi / funkcje nieukończone
Nie wszystkie typy danych są obsługiwane prawidłowo przy tworzeniu domen. W niektórych przypadkach domeny w nowej bazie mogą mieć inne typy niż w pierwotnej bazie.
