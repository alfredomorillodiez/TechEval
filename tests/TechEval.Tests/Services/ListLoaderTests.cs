using FluentAssertions;
using Xunit;
using TechEval.Application;
using TechEval.Domain.Common;

namespace TechEval.Tests.Services;

/// <summary>Spec admin-console: una respuesta tardía no sustituye a una reciente; la búsqueda espera.</summary>
public class ListLoaderTests
{
    [Fact]
    public async Task Una_carga_sola_se_aplica()
    {
        using var loader = new ListLoader();
        var aplicado = new List<int>();

        var ok = await loader.LoadAsync(() => Task.FromResult(7), aplicado.Add);

        ok.Should().BeTrue();
        aplicado.Should().Equal(7);
    }

    [Fact]
    public async Task Una_respuesta_lenta_anterior_no_sustituye_a_una_rapida_posterior()
    {
        using var loader = new ListLoader();
        var aplicado = new List<string>();
        var lenta = new TaskCompletionSource<string>();

        // La primera carga se queda esperando; la segunda empieza y termina antes.
        var primera = loader.LoadAsync(() => lenta.Task, aplicado.Add);
        var segunda = await loader.LoadAsync(() => Task.FromResult("rápida"), aplicado.Add);
        lenta.SetResult("lenta");
        var aplicoLaPrimera = await primera;

        segunda.Should().BeTrue();
        aplicoLaPrimera.Should().BeFalse();
        aplicado.Should().Equal("rápida");
    }

    [Fact]
    public async Task Despues_de_Dispose_una_respuesta_pendiente_no_se_aplica()
    {
        var loader = new ListLoader();
        var aplicado = new List<int>();
        var pendiente = new TaskCompletionSource<int>();

        var carga = loader.LoadAsync(() => pendiente.Task, aplicado.Add);
        loader.Dispose();
        pendiente.SetResult(1);

        (await carga).Should().BeFalse();
        aplicado.Should().BeEmpty();
    }

    [Fact]
    public async Task Cinco_pulsaciones_seguidas_hacen_una_sola_peticion()
    {
        using var loader = new ListLoader();
        var peticiones = 0;

        var tareas = Enumerable.Range(0, 5).Select(async _ =>
        {
            await loader.DebounceAsync(() => { peticiones++; return Task.CompletedTask; }, delayMs: 60);
        }).ToList();
        await Task.WhenAll(tareas);
        await Task.Delay(150);

        peticiones.Should().Be(1);
    }

    [Fact]
    public async Task Dos_pulsaciones_separadas_hacen_dos_peticiones()
    {
        using var loader = new ListLoader();
        var peticiones = 0;

        await loader.DebounceAsync(() => { peticiones++; return Task.CompletedTask; }, delayMs: 20);
        await loader.DebounceAsync(() => { peticiones++; return Task.CompletedTask; }, delayMs: 20);

        peticiones.Should().Be(2);
    }

    [Fact]
    public async Task Dispose_cancela_la_espera_pendiente()
    {
        var loader = new ListLoader();
        var peticiones = 0;

        var espera = loader.DebounceAsync(() => { peticiones++; return Task.CompletedTask; }, delayMs: 60);
        loader.Dispose();
        await espera;
        await Task.Delay(120);

        peticiones.Should().Be(0);
    }

    // ---------- PageState ----------

    private static PagedResult<int> Pagina(int items, int total, int page, int size)
        => new(Enumerable.Range(0, items).ToList(), total, page, size);

    [Fact]
    public void PageState_guarda_lo_que_devuelve_el_servidor()
    {
        var s = new PageState();

        s.Apply(Pagina(10, 60, 3, 25));

        (s.Page, s.PageSize, s.Total, s.TotalPages).Should().Be((3, 25, 60, 3));
    }

    [Theory]
    [InlineData(60, 1, 25, 1, 25)]
    [InlineData(60, 2, 25, 26, 50)]
    [InlineData(60, 3, 25, 51, 60)]
    [InlineData(0, 1, 25, 0, 0)]
    [InlineData(12, 1, 25, 1, 12)]
    public void PageState_rango_mostrado(int total, int page, int size, int primera, int ultima)
    {
        var s = new PageState();
        s.Apply(Pagina(0, total, page, size));

        (s.FirstRow, s.LastRow).Should().Be((primera, ultima));
    }

    [Fact]
    public void PageState_cambiar_el_tamano_vuelve_a_la_primera_pagina()
    {
        var s = new PageState { Page = 3 };

        s.SetPageSize(50);

        (s.Page, s.PageSize).Should().Be((1, 50));
    }

    [Fact]
    public void PageState_Reset_vuelve_a_la_primera()
    {
        var s = new PageState { Page = 4 };

        s.Reset();

        s.Page.Should().Be(1);
    }

    [Fact]
    public void PageState_detecta_una_pagina_guardada_que_ya_no_existe()
    {
        var s = new PageState { Page = 5 };
        var respuesta = Pagina(0, 60, 5, 25);

        s.IsBeyondEnd(respuesta).Should().BeTrue();
        s.Apply(respuesta);
        s.GoToLast();

        s.Page.Should().Be(3);
    }

    [Fact]
    public void PageState_una_busqueda_sin_resultados_no_es_una_pagina_pasada_del_final()
    {
        var s = new PageState();

        s.IsBeyondEnd(Pagina(0, 0, 1, 25)).Should().BeFalse();
        s.IsBeyondEnd(Pagina(10, 60, 3, 25)).Should().BeFalse();
    }

    [Fact]
    public void PageState_ToQuery_lleva_el_orden_y_el_texto()
    {
        var s = new PageState { Page = 2, PageSize = 50 };
        var orden = new SortState { Key = "score", Descending = true };

        var q = s.ToQuery("ana", orden);

        (q.Page, q.PageSize, q.Q, q.Sort, q.Desc).Should().Be((2, 50, "ana", "score", true));
    }
}
