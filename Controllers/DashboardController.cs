using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace ITSystem.Controllers
{
    public class DashboardController : Controller
    {
        private readonly AppDbContext _context;

        public DashboardController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {

            return View();
        }

        [HttpGet]
        public IActionResult ObtenerMetricasAnuales()
        {
            int anioActual = DateTime.Now.Year;

            // Rangos de fecha fijos para el año actual (Evita problemas de traducción de EF)
            DateTime inicioAnio = new DateTime(anioActual, 1, 1);
            DateTime finAnio = new DateTime(anioActual, 12, 31, 23, 59, 59);

            // Inicializamos arreglos de 12 posiciones (Ene a Dic)
            int[] creadosPorMes = new int[12];
            int[] cerradosPorMes = new int[12];

            // 1. Filtramos usando rangos de fechas directos, soportando si son nuleables o no
            var ticketsDelAnio = _context.Tickets
                .Where(t => (t.FechaCreacion >= inicioAnio && t.FechaCreacion <= finAnio) ||
                            (t.FechaCierre >= inicioAnio && t.FechaCierre <= finAnio))
                .ToList();

            // 2. Agrupar la información en memoria (aquí ya es seguro usar .Value porque está en RAM)
            foreach (var ticket in ticketsDelAnio)
            {
                // Contar creados (Validando si FechaCreacion es nuleable o no)
                if (ticket.FechaCreacion.HasValue)
                {
                    DateTime fCreacion = ticket.FechaCreacion.Value;
                    if (fCreacion.Year == anioActual)
                    {
                        int mesCreacion = fCreacion.Month - 1; // Indexado de 0 a 11
                        creadosPorMes[mesCreacion]++;
                    }
                }
                else if (ticket.FechaCreacion != null) // Por si tu propiedad no es nuleable en C#
                {
                    // Si el compilador detecta que NO es nuleable, usará este camino automáticamente
                    DateTime fCreacion = Convert.ToDateTime(ticket.FechaCreacion);
                    if (fCreacion.Year == anioActual)
                    {
                        creadosPorMes[fCreacion.Month - 1]++;
                    }
                }

                // Contar cerrados (Validando explícitamente FechaCierre)
                if (ticket.FechaCierre.HasValue && ticket.Estado == "Resuelto")
                {
                    DateTime fCierre = ticket.FechaCierre.Value;
                    if (fCierre.Year == anioActual)
                    {
                        int mesCierre = fCierre.Month - 1; // Indexado de 0 a 11
                        cerradosPorMes[mesCierre]++;
                    }
                }
            }

            // 3. Retornar el objeto con los datos listos para Chart.js
            return Ok(new
            {
                anio = anioActual,
                creados = creadosPorMes,
                cerrados = cerradosPorMes
            });
        }

        [HttpGet]
        public IActionResult ObtenerPorcentajeCumplimiento()
        {
            int anioActual = DateTime.Now.Year;
            DateTime inicioAnio = new DateTime(anioActual, 1, 1);
            DateTime finAnio = new DateTime(anioActual, 12, 31, 23, 59, 59);

            // 1. Traer todos los tickets creados en el año actual
            var ticketsDelAnio = _context.Tickets
                .Where(t => t.FechaCreacion >= inicioAnio && t.FechaCreacion <= finAnio)
                .ToList();

            int totalTickets = ticketsDelAnio.Count;
            int resueltos = ticketsDelAnio.Count(t => t.Estado == "Resuelto");
            int pendientesYOtros = totalTickets - resueltos; // En Proceso, En Espera, Nuevo, etc.

            // 2. Calcular porcentaje de forma segura (evitando división entre cero)
            double porcentaje = totalTickets > 0
                ? Math.Round(((double)resueltos / totalTickets) * 100, 1)
                : 0;

            return Ok(new
            {
                total = totalTickets,
                resueltos = resueltos,
                pendientes = pendientesYOtros,
                porcentajeCumplimiento = porcentaje
            });
        }

        [HttpGet]
        public IActionResult ObtenerTicketsPorTecnico()
        {
            // 1. Traer todos los usuarios/técnicos de la base de datos para mapear sus nombres
            var listaUsuariosBase = _context.Usuarios.ToList();
            var listaUsuarios = listaUsuariosBase.ToDictionary(
                u => u.ID,
                u => u.Nombre ?? "Técnico sin Nombre"
            );

            // 2. Consultar TODOS los tickets de la base de datos (tanto activos como cerrados)
            var todosLosTickets = _context.Tickets.ToList();

            // 3. Inicializar listas para almacenar la información final estructurada
            var nombresTecnicos = new List<string>();
            var totalesActivos = new List<int>();
            var totalesCerrados = new List<int>();

            // 4. Procesar primero los tickets de los técnicos registrados
            foreach (var usuario in listaUsuariosBase)
            {
                // Contamos tickets asignados activos (No Resueltos y No Cancelados)
                int activos = todosLosTickets.Count(t => t.UsuarioAsignadoId == usuario.ID &&
                                                         t.Estado != "Resuelto" &&
                                                         t.Estado != "Cancelado");

                // Contamos tickets que el técnico ya cerró con éxito
                int cerrados = todosLosTickets.Count(t => t.UsuarioAsignadoId == usuario.ID &&
                                                          t.Estado == "Resuelto");

                // Solo incluimos al técnico en la gráfica si tiene o ha tenido tickets asignados
                if (activos > 0 || cerrados > 0)
                {
                    nombresTecnicos.Add(usuario.Nombre ?? $"Usuario ID: {usuario.ID}");
                    totalesActivos.Add(activos);
                    totalesCerrados.Add(cerrados);
                }
            }

            // 5. Agregar la fila especial para los tickets que están "Sin Asignar"
            int activosSinAsignar = todosLosTickets.Count(t => !t.UsuarioAsignadoId.HasValue &&
                                                               t.Estado != "Resuelto" &&
                                                               t.Estado != "Cancelado");

            if (activosSinAsignar > 0)
            {
                nombresTecnicos.Add("Sin Asignar");
                totalesActivos.Add(activosSinAsignar);
                totalesCerrados.Add(0); // Lógicamente un ticket sin asignar no puede estar cerrado
            }

            // 6. Retornar las tres listas sincronizadas por posición a Chart.js
            return Ok(new
            {
                tecnicos = nombresTecnicos,
                activos = totalesActivos,
                cerrados = totalesCerrados
            });
        }

        [HttpGet]
        public IActionResult ObtenerTiemposPromedio()
        {
            int anioActual = DateTime.Now.Year;

            // Definimos el rango del año completo con DateTime puro
            DateTime inicioAnio = new DateTime(anioActual, 1, 1);
            DateTime finAnio = new DateTime(anioActual, 12, 31, 23, 59, 59);

            // Inicializamos arreglos para los 12 meses
            double[] promedioAtencionHoras = new double[12];
            double[] promedioSolucionDias = new double[12];

            // 1. Traer los tickets que fueron creados dentro del año actual de forma segura
            var ticketsDelAnio = _context.Tickets
                .Where(t => t.FechaCreacion >= inicioAnio && t.FechaCreacion <= finAnio)
                .ToList();

            // 2. Agrupar la información mes por mes en memoria RAM
            for (int mes = 1; mes <= 12; mes++)
            {
                // CORREGIDO: Usamos .Value.Month de forma segura ya que el filtro anterior garantiza que tienen valor
                var ticketsDelMes = ticketsDelAnio
                    .Where(t => t.FechaCreacion.HasValue && t.FechaCreacion.Value.Month == mes)
                    .ToList();

                // --- CÁLCULO 1: TIEMPO DE ATENCIÓN (De Creación a Proceso) ---
                var ticketsAtendidos = ticketsDelMes
                    .Where(t => t.FechaActualizacion.HasValue && t.Estado != "Nuevo" && t.Estado != "Por Atender")
                    .ToList();

                if (ticketsAtendidos.Any())
                {
                    // Calculamos la diferencia total en horas
                    double totalHoras = ticketsAtendidos.Sum(t => (t.FechaActualizacion.Value - t.FechaCreacion.Value).TotalHours);
                    promedioAtencionHoras[mes - 1] = Math.Round(totalHoras / ticketsAtendidos.Count, 1);
                }

                // --- CÁLCULO 2: TIEMPO DE SOLUCIÓN (De Creación a Cierre) ---
                var ticketsResueltos = ticketsDelMes
                    .Where(t => t.FechaCierre.HasValue && t.Estado == "Resuelto")
                    .ToList();

                if (ticketsResueltos.Any())
                {
                    // Calculamos la diferencia total en días
                    double totalDias = ticketsResueltos.Sum(t => (t.FechaCierre.Value - t.FechaCreacion.Value).TotalDays);
                    promedioSolucionDias[mes - 1] = Math.Round(totalDias / ticketsResueltos.Count, 1);
                }
            }

            return Ok(new
            {
                atencion = promedioAtencionHoras,
                solucion = promedioSolucionDias
            });
        }

        [HttpGet]
        public IActionResult ObtenerVolumenPorEstado()
        {
            try
            {
                // Calculamos de forma dinámica el año en curso
                int anioActual = DateTime.Now.Year;
                DateTime inicioAnio = new DateTime(anioActual, 1, 1);
                DateTime finAnio = new DateTime(anioActual, 12, 31, 23, 59, 59);

                // 1. Descargamos la lista base filtrando por rango de fechas de forma segura 
                // contemplando si el campo es nuleable en la base de datos.
                var ticketsDelAnio = _context.Tickets
                    .Where(t => t.FechaCreacion.HasValue &&
                                t.FechaCreacion.Value >= inicioAnio &&
                                t.FechaCreacion.Value <= finAnio)
                    .Select(t => new { t.Estado }) // Proyección ultra ligera
                    .ToList();

                // 2. Contabilizamos el acumulado anual por cada fase de tu flujo (Manejo de nulos preventivo)
                int porAtender = ticketsDelAnio.Count(e => e.Estado == "Por Atender" || e.Estado == "Nuevo");
                int enProceso = ticketsDelAnio.Count(e => e.Estado == "En Proceso");
                int enEspera = ticketsDelAnio.Count(e => e.Estado == "En Espera");
                int resueltos = ticketsDelAnio.Count(e => e.Estado == "Resuelto");

                return Ok(new
                {
                    estados = new string[] { "Por Atender", "En Proceso", "En Espera", "Resuelto" },
                    totales = new int[] { porAtender, enProceso, enEspera, resueltos }
                });
            }
            catch (Exception ex)
            {
                // Si hay algún problema con los nombres de las columnas, esto te dirá exactamente qué pasó en la consola de Visual Studio
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerVolumenPorEstado: {ex.Message}");
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }

        [HttpGet]
        public IActionResult ObtenerEfectividadResolucion()
        {
            // 1. Obtener únicamente los tickets que ya se encuentran en estado Resuelto
            var ticketsResueltos = _context.Tickets
                .Where(t => t.Estado == "Resuelto")
                .ToList();

            int primeraVisita = 0;
            int segundaVisita = 0;
            int tiempoExtendido = 0;

            // 2. Clasificar cada ticket según el número de renglones/bloques en su bitácora de comentarios
            foreach (var ticket in ticketsResueltos)
            {
                if (string.IsNullOrEmpty(ticket.Comentarios))
                {
                    // Si por alguna razón no tiene bitácora pero está resuelto, se considera directo
                    primeraVisita++;
                    continue;
                }

                // Dividimos las líneas de la bitácora física
                var lineasBitacora = ticket.Comentarios
                    .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(linea => !string.IsNullOrWhiteSpace(linea))
                    .ToList();

                // Contamos cuántas interacciones/notas se registraron (excluyendo el texto automático de apertura o el de resolución)
                // Un ticket óptimo solo tiene: [Ticket Tomado] y [Ticket Resuelto] (2 líneas)
                int numeroInteracciones = lineasBitacora.Count;

                if (numeroInteracciones <= 2)
                {
                    primeraVisita++;
                }
                else if (numeroInteracciones == 3)
                {
                    segundaVisita++;
                }
                else
                {
                    tiempoExtendido++;
                }
            }

            int totalResueltos = ticketsResueltos.Count;

            // Calcular el porcentaje de FCR (First Contact Resolution)
            double porcentajeFcr = totalResueltos > 0
                ? Math.Round(((double)primeraVisita / totalResueltos) * 100, 1)
                : 0;

            return Ok(new
            {
                total = totalResueltos,
                primera = primeraVisita,
                segunda = segundaVisita,
                extendido = tiempoExtendido,
                fcr = porcentajeFcr
            });
        }

        [HttpGet]
        public IActionResult ObtenerResueltosPorCategoriaApilada()
        {
            try
            {
                int anioActual = DateTime.Now.Year;
                DateTime inicioAnio = new DateTime(anioActual, 1, 1);
                DateTime finAnio = new DateTime(anioActual, 12, 31, 23, 59, 59);

                // 1. Descargar los tickets resueltos del año
                var ticketsDelAnio = _context.Tickets
                    .Where(t => t.Estado == "Resuelto" &&
                                t.FechaCreacion.HasValue &&
                                t.FechaCreacion.Value >= inicioAnio &&
                                t.FechaCreacion.Value <= finAnio)
                    .ToList();

                // 2. Traer catálogos base de IDs a Nombres
                var listaCategorias = _context.Categorias.ToDictionary(c => c.Id, c => c.Nombre ?? "General");
                var listaSubcategorias = _context.Subcategorias.ToDictionary(s => s.Id, s => s.Nombre ?? "General");

                // 3. Traducir y aplanar los datos en objetos limpios en memoria RAM
                var ticketsMapeados = ticketsDelAnio.Select(t =>
                {
                    int catId = 0; int subId = 0;
                    var propiedades = t.GetType().GetProperties();

                    var propCat = propiedades.FirstOrDefault(p => p.Name.Equals("CategoriaId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("CategoriaID", StringComparison.OrdinalIgnoreCase));
                    if (propCat != null) catId = Convert.ToInt32(propCat.GetValue(t) ?? 0);

                    var propSub = propiedades.FirstOrDefault(p => p.Name.Equals("SubcategoriaId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("SubcategoriaID", StringComparison.OrdinalIgnoreCase));
                    if (propSub != null) subId = Convert.ToInt32(propSub.GetValue(t) ?? 0);

                    return new
                    {
                        Categoria = listaCategorias.ContainsKey(catId) ? listaCategorias[catId] : "General",
                        Subcategoria = listaSubcategorias.ContainsKey(subId) ? listaSubcategorias[subId] : "General"
                    };
                }).ToList();

                // 4. Obtener listas únicas de Categorías (Eje X) y Subcategorías (Bloques apilados)
                var categoriasUnicas = ticketsMapeados.Select(x => x.Categoria).Distinct().ToList();
                var subcategoriasUnicas = ticketsMapeados.Select(x => x.Subcategoria).Distinct().ToList();

                // 5. Construir los conjuntos de datos (Datasets) requeridos por Chart.js
                var datasetsFinales = new List<object>();

                foreach (var sub in subcategoriasUnicas)
                {
                    var dataValores = new List<int>();

                    foreach (var cat in categoriasUnicas)
                    {
                        // Contamos cuántos tickets pertenecen a esta combinación exacta de Categoría y Subcategoría
                        int conteo = ticketsMapeados.Count(x => x.Categoria == cat && x.Subcategoria == sub);
                        dataValores.Add(conteo);
                    }

                    datasetsFinales.Add(new
                    {
                        label = sub, // Nombre de la subcategoría en la leyenda
                        data = dataValores // Lista de conteos alineada con el orden de las categorías
                    });
                }

                return Ok(new
                {
                    categorias = categoriasUnicas,
                    datasets = datasetsFinales
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error al procesar barras apiladas: {ex.Message}");
            }
        }


    }
}
