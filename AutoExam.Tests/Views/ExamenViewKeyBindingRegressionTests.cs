using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using AutoExam.Tests.TestDoubles;
using AutoExam.Tests.TestSupport;
using AutoExam.Views;
using AutoExam.Models;

namespace AutoExam.Tests.Views;

/// <summary>
/// Suite de regresión de los <c>KeyBinding</c> YA EXISTENTES en Views/ExamenView.xaml —
/// NFR-09/NFR-10, AC-T11 (specs/02-tech-spec.md), contrato en specs/03-architecture.md §4.4.
///
/// Es la red de seguridad para <c>dev-ventana-navegacion</c>: los atajos nuevos de
/// navegación (Ctrl+1..Ctrl+5) se declaran en <c>Window.InputBindings</c> de
/// <c>MainWindow.xaml</c>, un ancestro de esta vista en el árbol visual real de la app.
/// Si al sumarlos algo se filtra por burbujeo de eventos de teclado (un manejador que marca
/// <c>Handled</c> de más, un cambio de foco que ya no deja el foco dentro de esta vista, un
/// <c>CommandParameter</c> que se pisa), esta suite debe pasar de verde a rojo — corriéndola
/// hoy, ANTES de esos cambios, se fija la línea de base.
///
/// Cada test corre marshaleado al hilo STA único que expone <see cref="WpfHost"/>
/// (<see cref="WpfHost.Invocar(Action)"/>): los objetos WPF tienen afinidad de hilo y
/// <c>Application.Current</c> es un singleton de todo el proceso, así que no se puede usar un
/// hilo STA nuevo por test (lo que hacen los "[WpfFact]" de paquetes como Xunit.StaFact) sin
/// romper esa afinidad en el segundo test que corre.
///
/// Dos niveles de test, deliberadamente no redundantes (DoD del rol):
///   - <see cref="InputBindings_CoincideExactoConElContratoNFR09"/> prueba el CONTRATO
///     DECLARADO en el XAML (qué tecla está atada a qué comando/parámetro). Solo detecta
///     ediciones directas de ExamenView.xaml.
///   - Los tests de ejecución (<see cref="CadaAtajo_ConFocoEnLaVista_DisparaSoloElComandoEsperado"/>
///     y compañía) prueban el COMPORTAMIENTO EN TIEMPO DE EJECUCIÓN (que WPF de verdad invoque
///     el comando al burbujear la tecla). Es lo único que puede detectar una regresión causada
///     por un archivo DISTINTO (MainWindow.xaml) que ExamenView nunca declara como dependencia
///     — exactamente el riesgo que motiva esta suite.
/// </summary>
public class ExamenViewKeyBindingRegressionTests
{
    /// <summary>
    /// Tabla NFR-09, tomada literal de Views/ExamenView.xaml (UserControl.InputBindings):
    /// 1-4, NumPad1-4, A-D → ResponderCommand con el índice de opción (0-3);
    /// Right/Enter → SiguienteCommand; Left → AnteriorCommand; S → SaltearCommand.
    /// </summary>
    public static IEnumerable<object?[]> AtajosEsperados()
    {
        yield return new object?[] { Key.D1, "ResponderCommand", "0" };
        yield return new object?[] { Key.D2, "ResponderCommand", "1" };
        yield return new object?[] { Key.D3, "ResponderCommand", "2" };
        yield return new object?[] { Key.D4, "ResponderCommand", "3" };
        yield return new object?[] { Key.NumPad1, "ResponderCommand", "0" };
        yield return new object?[] { Key.NumPad2, "ResponderCommand", "1" };
        yield return new object?[] { Key.NumPad3, "ResponderCommand", "2" };
        yield return new object?[] { Key.NumPad4, "ResponderCommand", "3" };
        yield return new object?[] { Key.A, "ResponderCommand", "0" };
        yield return new object?[] { Key.B, "ResponderCommand", "1" };
        yield return new object?[] { Key.C, "ResponderCommand", "2" };
        yield return new object?[] { Key.D, "ResponderCommand", "3" };
        yield return new object?[] { Key.Right, "SiguienteCommand", null };
        yield return new object?[] { Key.Enter, "SiguienteCommand", null };
        yield return new object?[] { Key.Left, "AnteriorCommand", null };
        yield return new object?[] { Key.S, "SaltearCommand", null };
    }

    // ------------------------------------------------------------------
    // Nivel 1 — contrato declarado en el XAML
    // ------------------------------------------------------------------

    [Fact]
    public void ElMapaDeAtajos_CoincideExactoConElContratoNFR09()
    {
        // Este test miraba los KeyBinding declarados en ExamenView.xaml. US-036 los movio a
        // un mapeo centralizado (RN-44: "no hardcodeado disperso por vista"), asi que ahora
        // mira ese mapeo. La garantia no cambio —que tecla dispara que accion, sin
        // modificadores— y de hecho quedo mas fuerte: antes se podia agregar una tecla en el
        // XAML sin que nada verificara que 1 y A apuntaran a la misma opcion.
        var declarados = AtajosExamen.Todos
            .Select(a => new
            {
                a.Tecla,
                Comando = a.Accion switch
                {
                    AccionAtajo.Responder => "ResponderCommand",
                    AccionAtajo.Siguiente => "SiguienteCommand",
                    AccionAtajo.Anterior => "AnteriorCommand",
                    AccionAtajo.Saltear => "SaltearCommand",
                    _ => "?"
                },
                Parametro = a.EsDeOpcion ? a.Opcion.ToString() : null,
            })
            .ToList();

        var esperados = AtajosEsperados()
            .Select(fila => new { Key = (Key)fila[0]!, Comando = (string)fila[1]!, Parametro = (string?)fila[2] })
            .ToList();

        // Cantidad exacta primero: si alguien agrega o saca un atajo sin querer, el mensaje de
        // fallo tiene que decir "16 vs N", no perderse en el Contains de abajo.
        Assert.Equal(esperados.Count, declarados.Count);

        foreach (var esperado in esperados)
        {
            Assert.Contains(declarados, d =>
                d.Tecla == esperado.Key &&
                d.Comando == esperado.Comando &&
                d.Parametro == esperado.Parametro);
        }
    }

    [Fact]
    public void LaVista_YaNoDeclaraAtajosPorSuCuenta_RN44()
    {
        // RN-44: el mapeo vive en un solo lugar. Si alguien vuelve a agregar un KeyBinding
        // aca, habria dos fuentes de verdad y la referencia que se le muestra al alumno
        // (armada desde AtajosExamen) dejaria de decir la verdad.
        WpfHost.Invocar(() =>
        {
            WpfHost.AsegurarRecursos();
            var vista = new ExamenView();

            Assert.Empty(vista.InputBindings.OfType<KeyBinding>());
        });
    }

    [Fact]
    public void LaReferenciaQueSeLeMuestraAlAlumno_SaleDelMismoMapa_US036()
    {
        // El criterio pide "una referencia visible pero discreta de que atajos existen". Que
        // se arme del mismo lugar que las teclas es lo que evita el caso clasico: una ayuda
        // que sigue nombrando una tecla que ya no hace nada.
        Assert.NotEmpty(AtajosExamen.Referencia);

        string todo = string.Join(" ", AtajosExamen.Referencia.Select(r => r.Teclas));

        Assert.Contains("1", todo, StringComparison.Ordinal);
        Assert.Contains("A", todo, StringComparison.Ordinal);
        Assert.Contains("S", todo, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------
    // Nivel 2 — comportamiento real al burbujear la tecla
    // ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(AtajosEsperados))]
    public void CadaAtajo_ConFocoEnLaVista_DisparaSoloElComandoEsperado(Key tecla, string comandoEsperado, string? parametroEsperado)
    {
        WpfHost.Invocar(() =>
        {
            WpfHost.AsegurarRecursos();
            var vm = new ExamenViewFakeViewModel();
            var vista = new ExamenView { DataContext = vm };

            // El código de ExamenView.xaml.cs deja el foco en un descendiente de la vista al
            // hacerse visible (ver comentario en el XAML); levantar la tecla desde la vista
            // misma reproduce ese caso — el evento burbujea desde ella igual que burbujearía
            // desde cualquier descendiente enfocado.
            bool manejado = WpfHost.RaiseKeyDown(vista, tecla);

            Assert.True(manejado);

            foreach (var (nombre, comando) in vm.Comandos)
            {
                if (nombre == comandoEsperado)
                {
                    Assert.Equal(1, comando.Invocaciones);
                    Assert.Equal(parametroEsperado, comando.UltimoParametro as string);
                }
                else
                {
                    Assert.Equal(0, comando.Invocaciones);
                }
            }
        });
    }

    [Fact]
    public void TeclaSinAtajoAsignado_NoDisparaNingunComando()
    {
        // Control negativo: prueba que el fixture no da falsos positivos (p.ej. si
        // RaiseKeyDown quedara mal armado y disparara cualquier binding sin filtrar por tecla).
        WpfHost.Invocar(() =>
        {
            WpfHost.AsegurarRecursos();
            var vm = new ExamenViewFakeViewModel();
            var vista = new ExamenView { DataContext = vm };

            WpfHost.RaiseKeyDown(vista, Key.Z);

            Assert.All(vm.Comandos.Values, c => Assert.Equal(0, c.Invocaciones));
        });
    }

    // ------------------------------------------------------------------
    // NFR-10 — no hay superficie editable dentro de esta vista que compita con los atajos
    // ------------------------------------------------------------------

    [Fact]
    public void LaVistaNoEsFocusable_ElFocoQuedaSiempreEnUnDescendiente()
    {
        // Ver el comentario en Views/ExamenView.xaml: si este UserControl pasara a ser
        // Focusable, el foco rebotaría del botón al UserControl durante el clic y
        // ButtonBase dejaría de convertir el MouseUp en Click — regresión de mouse, no solo
        // de teclado. UserControl ya es no-Focusable por default (a diferencia de Control);
        // este test protege que nadie lo pise agregando Focusable="True".
        WpfHost.Invocar(() =>
        {
            WpfHost.AsegurarRecursos();
            var vista = new ExamenView();

            Assert.False(vista.Focusable);
        });
    }

    [Fact]
    public void NingunTextBoxDeLaVistaEsEditable()
    {
        // NFR-10 para ExamenView específicamente: no hay ningún campo de texto donde tipear
        // "1"/"a"/"s" compita con los atajos, porque los únicos TextBox de esta vista son de
        // solo lectura (estilo TextoSeleccionable, para copiar el enunciado). Si alguna vez se
        // agrega un TextBox editable a esta vista, este test lo marca: haría falta blindar el
        // KeyBinding correspondiente contra el foco en ese campo, igual que va a tener que
        // hacer dev-ventana-navegacion con Ctrl+1..5 en MainWindow (ver
        // specs/03-architecture.md §4.4, guardia de TextBoxBase/PasswordBox).
        WpfHost.Invocar(() =>
        {
            WpfHost.AsegurarRecursos();
            var vista = new ExamenView();

            var textBoxes = BuscarDescendientes<TextBox>(vista).ToList();

            Assert.NotEmpty(textBoxes); // si esto da 0, el test de arriba ya no prueba nada real
            Assert.All(textBoxes, tb => Assert.True(tb.IsReadOnly, $"TextBox editable encontrado (Name='{tb.Name}'): rompe el supuesto detrás de NFR-10 en esta vista."));
        });
    }

    private static IEnumerable<T> BuscarDescendientes<T>(DependencyObject raiz) where T : DependencyObject
    {
        foreach (object hijo in LogicalTreeHelper.GetChildren(raiz))
        {
            if (hijo is not DependencyObject hijoDo)
            {
                continue;
            }

            if (hijoDo is T encontrado)
            {
                yield return encontrado;
            }

            foreach (var nieto in BuscarDescendientes<T>(hijoDo))
            {
                yield return nieto;
            }
        }
    }
}
