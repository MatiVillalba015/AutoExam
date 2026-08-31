# Test plan — M3 (modelo `Fuente`/`Libro` generalizado + `BibliotecaService`)

Alcance: solo el módulo `dev-modelo-fuente-biblioteca` (US-008/009/010, contrato en
`03-architecture.md` Inc-4 §3 y §4.2). Los AC-T de extracción (M1), imagen/HEIC (M2), ingesta/UI
(M4) y resultado/historial (M5) los cubren sus test-devs pares — acá se listan solo para dejar
explícito qué parte del AC-T valida M3 y qué queda delegado.

Contract-first: los tests referencian `TipoFuente`, `Libro.Tipo/Archivos/MedidaTamanio`,
`BibliotecaService.AgregarFuenteAsync`, `FactoriaExtractores`, `FuenteIlegibleException` tal como
los fija la arquitectura. Compilan cuando M1 publica el contrato de `IExtractorContenido`/`FactoriaExtractores`
y M3 la forma final de `Libro` — el propio build es el gate de "el dev implementó el contrato exacto".

## Matriz de cobertura

| AC-T / NFR | Qué valida M3 | Archivo::caso | Delegado a |
|---|---|---|---|
| AC-T41 / NFR-40 | `MedidaTamanio` poblada tras el alta (PDF → "N páginas", set-imágenes → "N imágenes"); 0 fuentes sin medida | `BibliotecaServiceAgregarFuenteTests` :: `AgregarFuente_Pdf_*MedidaTamanio*`, `*SetImagenes*MedidaEnImagenes` | medida Office (Word/Excel/PPT) → `test-dev-extraccion-multiformato` |
| AC-T43 / NFR-37, NFR-41 | fuente ilegible/dañada → se borra la copia parcial y se re-lanza con causa; **0** entradas en `Libros`; **0** archivos/carpetas en `Biblioteca` | `BibliotecaServiceAgregarFuenteTests` :: `AgregarFuente_PdfDañado_*`, `*Inexistente*`, `*MezclaDeTipos_ArgumentException_*` | mensaje exacto de `.doc/.xls/.ppt` y de formato desconocido → `FactoriaExtractores` (M1) |
| AC-T48 (parcial) / NFR-43 | set de N imágenes = **1** fuente `SetImagenes`; orden de `Archivos` = orden de alta (fuentes con nombres no alfabéticos); copia interna `Biblioteca\{Id}\NN.ext` correlativa | `BibliotecaServiceAgregarFuenteTests` :: `AgregarFuente_SetImagenes_PreservaOrdenDeAlta`, `*CopiaNumeradaCorrelativa` | selección múltiple en el diálogo / regla "no se combinan tipos" en el VM → `test-dev-ingesta-y-alcance` (M4); recorte al superar `MaxImagenesPorMaterial` y conversión HEIC → `test-dev-fuentes-imagen-heic` (M2) |
| NFR-A8 | persistencia solo vía `JsonStore`+`RutasApp`; `EliminarLibro` borra archivo (tipo único) o carpeta (set-imágenes) y persiste | `BibliotecaServiceAgregarFuenteTests` :: `EliminarLibro_*` | — |
| Migración `libros.json` (Inc-4 §3) | registro viejo sin `tipo`/`archivos` (clave ausente **o** `null` explícito) → `Tipo = Pdf`, `Archivos = [RutaArchivo]`, resto de campos intacto | `LibroFuenteGeneralizadaTests` (nivel JSON puro) + `BibliotecaServiceMigracionTests` (nivel `Cargar()`) | — |
| Forma de `Libro` (Inc-4 §3/§4.2) | defaults (`Tipo=Pdf`, `Archivos=[]`, `MedidaTamanio=""`); round-trip `JsonStore`; `RutaArchivo == Archivos[0]` para tipo de archivo único | `LibroFuenteGeneralizadaTests` | consumo de la forma en el paso Alcance → M4 |

## Casos edge no obvios (justifican este documento)

1. **Migración con `"archivos": null` explícito vs. clave ausente.** System.Text.Json pisa el
   inicializador `= new()` con `null` cuando la clave está presente con valor `null`; con la
   clave ausente lo deja en `[]`. `Cargar()` debe back-fillear en ambos casos. Se testean por
   separado.
2. **Orden de alta ≠ orden alfabético del archivo origen.** Un set `["z.png","a.png","m.png"]`
   debe copiarse a `01`(z) `02`(a) `03`(m). Testear con nombres que rompan el orden natural
   evita un falso verde si la implementación ordena por nombre.
3. **Limpieza de copia parcial ante fallo.** Si `MedirAsync` lanza (PDF dañado) después de haber
   copiado el archivo/carpeta a `Biblioteca`, no debe quedar basura ni una entrada "vacía"
   (AC-T43: "0 fuentes vacías creadas"). Se verifica el estado del filesystem, no solo la
   excepción.
4. **Mezcla de familias.** `["foto.png","apunte.pdf"]` → `ArgumentException` (traducida a aviso
   por el VM), sin copiar nada. Distinto de "formato no soportado" (`FormatoNoSoportadoException`,
   responsabilidad de M1).
5. **`AgregarLibroAsync` como wrapper.** El call-site viejo (un solo PDF) debe seguir dando un
   `Libro` con `Tipo=Pdf`, `Archivos.Count==1`, `Modulos` poblados igual que antes (NFR-A5:
   los call-sites existentes no se rompen).
