# Touren: Sortierung und Paging (Design)

`TourenListenfilter(DateOnly? Von, DateOnly? Bis, long? AktivitaetId, long? KategorieId)` is positional in `AndyTours.Contracts/V1/TourenVertrag.cs`; preserve its constructor and add optional init-only typed sort/direction fields with a documented legacy default. `ITourenSpeicher.ListeGefiltert` is the application seam. `SqliteTourenSpeicher.ListeGefiltert` currently filters into a list and applies `Skip/Take` without an explicit order, so page membership is not stable. Validate a finite enum/order whitelist and sort server-side before paging, always adding `Id` as a stable tie-breaker; never interpolate client member names into SQL.

`ApiHauptfenster.xaml` currently exposes six columns: Datum, Startzeit, Name, Distanz, Höhenmeter, Fahrzeit. Its `DataGrid` has no server-sort handler; WPF header sorting would affect only the loaded page. Add an explicit typed query contract and client request mapping, then handle header selection in the API view-model/window with cancellation, stale-response suppression, and reset to page one.

Legacy sorting semantics could not be verified: no legacy tour-grid source or sort handler is present in this checkout. Do not assert culture/collation or historical default until that source is inspected. Current API grid has six columns; full legacy parity is not established.
