using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using AutoExam.Behaviors;
using AutoExam.Tests.TestSupport;

namespace AutoExam.Tests.Ingesta;

/// <summary>
/// <c>Behaviors/SoltarArchivo.cs</c> tras M4 — specs/03-architecture.md (Incremento 4) §3/§4.4:
/// <c>Extension</c> (string singular <c>".pdf"</c>) pasa a <c>Extensiones</c> (lista separada por
/// espacios), <c>PrimerArchivoValido</c> pasa a <c>ArchivosValidos</c> (devuelve <c>string[]</c>)
/// y el <c>Drop</c> ejecuta el comando con TODAS las rutas válidas soltadas. Cubre AC-T48
/// (selección múltiple para un material), AC-T51 / NFR-43 (orden de alta preservado), NFR-37
/// (extensión no admitida filtrada, 0 fuentes).
///
/// Todo corre marshaleado al hilo STA único de <see cref="WpfHost"/>: <see cref="DataObject"/> y
/// cualquier <see cref="DependencyObject"/> tienen afinidad de hilo STA.
/// </summary>
public class SoltarArchivoMultiArchivoTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(), "AutoExam.Tests.Soltar", Guid.NewGuid().ToString("N"));

    public SoltarArchivoMultiArchivoTests() => Directory.CreateDirectory(_carpeta);

    public void Dispose()
    {
        try { Directory.Delete(_carpeta, recursive: true); } catch { /* best-effort */ }
    }

    private string CrearArchivo(string nombre)
    {
        var ruta = Path.Combine(_carpeta, nombre);
        File.WriteAllText(ruta, "x");
        return ruta;
    }

    // ------------------------------------------------------------------
    // Forma del contrato (§4.4)
    // ------------------------------------------------------------------

    [Fact]
    public void ExponeElAccesorAdjuntoExtensiones_YYaNoExtensionSingular()
    {
        var getExtensiones = typeof(SoltarArchivo).GetMethod(
            "GetExtensiones", BindingFlags.Public | BindingFlags.Static);
        var getExtensionViejo = typeof(SoltarArchivo).GetMethod(
            "GetExtension", BindingFlags.Public | BindingFlags.Static);

        Assert.True(getExtensiones is not null,
            "SoltarArchivo.GetExtensiones(DependencyObject) no existe — §4.4 renombra Extension → Extensiones.");
        Assert.Equal(typeof(string), getExtensiones!.ReturnType);
        Assert.True(getExtensionViejo is null,
            "SoltarArchivo.GetExtension sigue existiendo — §4.4 lo reemplaza por Extensiones (lista).");
    }

    [Fact]
    public void ExtensionesProperty_EsUnaDependencyPropertyAdjunta()
    {
        var dp = typeof(SoltarArchivo).GetField(
            "ExtensionesProperty", BindingFlags.Public | BindingFlags.Static);

        Assert.True(dp is not null, "SoltarArchivo.ExtensionesProperty no existe (§4.4).");
        Assert.Equal(typeof(DependencyProperty), dp!.FieldType);
    }

    // ------------------------------------------------------------------
    // Filtrado multi-archivo (§3: ArchivosValidos devuelve string[])
    // ------------------------------------------------------------------

    /// <summary>Método privado que filtra el FileDrop: forma <c>(DependencyObject, IDataObject) →
    /// IEnumerable&lt;string&gt;</c> en cualquier orden de parámetros. Se busca por forma, no por
    /// nombre, para sobrevivir a que el developer lo llame <c>ArchivosValidos</c>, <c>RutasValidas</c>,
    /// etc.</summary>
    private static MethodInfo ResolverFiltro()
    {
        bool EsEnumerableDeString(Type t) =>
            t != typeof(string) &&
            typeof(IEnumerable).IsAssignableFrom(t) &&
            (t == typeof(string[]) ||
             t.GetInterfaces().Append(t).Any(i =>
                 i.IsGenericType &&
                 i.GetGenericTypeDefinition() == typeof(IEnumerable<>) &&
                 i.GetGenericArguments()[0] == typeof(string)));

        var candidatos = typeof(SoltarArchivo)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Where(m =>
            {
                var ps = m.GetParameters().Select(p => p.ParameterType).ToHashSet();
                return ps.Count == 2 &&
                       ps.Contains(typeof(DependencyObject)) &&
                       ps.Contains(typeof(IDataObject)) &&
                       EsEnumerableDeString(m.ReturnType);
            })
            .ToList();

        return candidatos.Count switch
        {
            1 => candidatos[0],
            0 => throw new InvalidOperationException(
                "No se encontró en SoltarArchivo un método (DependencyObject, IDataObject) → IEnumerable<string> — " +
                "§3 del contrato: PrimerArchivoValido pasa a ArchivosValidos y devuelve string[]."),
            _ => throw new InvalidOperationException(
                $"Se encontraron {candidatos.Count} métodos con esa forma en SoltarArchivo; el contrato espera uno solo."),
        };
    }

    private static string[] Filtrar(DependencyObject destino, IDataObject datos)
    {
        var metodo = ResolverFiltro();
        var resultado = metodo.Invoke(
            metodo.IsStatic ? null : Activator.CreateInstance(typeof(SoltarArchivo)),
            new object?[] { EnOrden(metodo, destino, datos) is var args ? args[0] : null, args[1] });
        return ((IEnumerable)resultado!).Cast<object>().Select(x => x!.ToString()!).ToArray();
    }

    private static object?[] EnOrden(MethodInfo metodo, DependencyObject destino, IDataObject datos)
        => metodo.GetParameters()[0].ParameterType == typeof(DependencyObject)
            ? new object?[] { destino, datos }
            : new object?[] { datos, destino };

    [Fact] // AC-T48 + AC-T51/NFR-43 (orden) + NFR-37 (filtrado)
    public void ArchivosValidos_DevuelveSoloLosExistentesConExtensionAdmitida_EnOrdenDeAlta()
    {
        WpfHost.Invocar(() =>
        {
            var pdf = CrearArchivo("a.pdf");
            var png = CrearArchivo("b.PNG");      // mayúsculas: debe entrar igual
            var heic = CrearArchivo("c.heic");
            var txt = CrearArchivo("d.txt");      // extensión no admitida
            var docx = CrearArchivo("e.docx");    // válida para la app pero no en la lista de este elemento
            var faltante = Path.Combine(_carpeta, "f.pdf"); // no existe en disco

            var zona = new Border();
            SoltarArchivo.SetExtensiones(zona, ".pdf .png .heic");

            var datos = new DataObject(DataFormats.FileDrop,
                new[] { docx, pdf, faltante, png, heic, txt });

            var validos = Filtrar(zona, datos);

            Assert.Equal(new[] { pdf, png, heic }, validos);
        });
    }

    [Fact] // NFR-37 — ninguna válida ⇒ colección vacía, nunca null (el VM no crea fuente)
    public void ArchivosValidos_SinNingunaValida_DevuelveColeccionVacia()
    {
        WpfHost.Invocar(() =>
        {
            var txt = CrearArchivo("solo.txt");
            var zona = new Border();
            SoltarArchivo.SetExtensiones(zona, ".pdf .docx");

            var datos = new DataObject(DataFormats.FileDrop, new[] { txt });

            var validos = Filtrar(zona, datos);

            Assert.NotNull(validos);
            Assert.Empty(validos);
        });
    }

    [Fact] // paridad con el comportamiento previo: Extensiones vacío ⇒ acepta cualquier archivo existente
    public void ArchivosValidos_ConExtensionesVacio_AceptaCualquierArchivoExistente()
    {
        WpfHost.Invocar(() =>
        {
            var a = CrearArchivo("cualquiera.xyz");
            var b = CrearArchivo("otro.zzz");
            var zona = new Border();
            SoltarArchivo.SetExtensiones(zona, string.Empty);

            var datos = new DataObject(DataFormats.FileDrop, new[] { a, b });

            Assert.Equal(new[] { a, b }, Filtrar(zona, datos));
        });
    }

    [Fact] // el drop de un solo archivo sigue funcionando (array de 1, §3)
    public void ArchivosValidos_ConUnSoloArchivo_DevuelveArrayDeUno()
    {
        WpfHost.Invocar(() =>
        {
            var pdf = CrearArchivo("unico.pdf");
            var zona = new Border();
            SoltarArchivo.SetExtensiones(zona, ".pdf");

            var datos = new DataObject(DataFormats.FileDrop, new[] { pdf });

            Assert.Equal(new[] { pdf }, Filtrar(zona, datos));
        });
    }
}
