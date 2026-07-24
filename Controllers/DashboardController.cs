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
        public IActionResult ObtenerMetricasAnuales(DateTime? inicio, DateTime? fin)
        {
            try
            {
                int anioActual = DateTime.Now.Year;

                // 1. Configurar rango de fechas dinámico (Año actual completo por defecto)
                DateTime fechaInicio = inicio ?? new DateTime(anioActual, 1, 1);
                DateTime fechaFin = fin ?? DateTime.Now;

                // Forzamos el último segundo del día seleccionado para incluir todos los registros
                fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59);

                // Inicializamos arreglos fijados de 12 posiciones (Ene a Dic)
                int[] creadosPorMes = new int[12];
                int[] cerradosPorMes = new int[12];

                // 2. Filtramos de manera segura validando nulos a nivel SQL antes del ToList
                var ticketsDelRango = _context.Tickets
                    .Where(t => (t.FechaCreacion.HasValue && t.FechaCreacion.Value >= fechaInicio && t.FechaCreacion.Value <= fechaFin) ||
                                (t.FechaCierre.HasValue && t.FechaCierre.Value >= fechaInicio && t.FechaCierre.Value <= fechaFin))
                    .ToList(); // Evaluamos de forma segura en memoria RAM

                // 3. Agrupar la información en los meses correspondientes (0 a 11)
                foreach (var ticket in ticketsDelRango)
                {
                    // Contar creados si entran en el rango de fechas solicitado
                    if (ticket.FechaCreacion.HasValue)
                    {
                        DateTime fCreacion = ticket.FechaCreacion.Value;
                        if (fCreacion >= fechaInicio && fCreacion <= fechaFin)
                        {
                            int mesCreacion = fCreacion.Month - 1; // Indexado de 0 a 11
                            creadosPorMes[mesCreacion]++;
                        }
                    }

                    // Contar cerrados si están resueltos y entran en el rango de fechas solicitado
                    if (ticket.FechaCierre.HasValue && ticket.Estado == "Resuelto")
                    {
                        DateTime fCierre = ticket.FechaCierre.Value;
                        if (fCierre >= fechaInicio && fCierre <= fechaFin)
                        {
                            int mesCierre = fCierre.Month - 1; // Indexado de 0 a 11
                            cerradosPorMes[mesCierre]++;
                        }
                    }
                }

                // 4. Retornar el objeto con los datos listos para Chart.js
                return Ok(new
                {
                    anio = anioActual,
                    creados = creadosPorMes,
                    cerrados = cerradosPorMes
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerMetricasAnuales: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpGet]
        public IActionResult ObtenerTicketsPorTecnico(DateTime? inicio, DateTime? fin)
        {
            try
            {
                // 1. Configurar rango de fechas de forma explícita y directa
                int anioActual = DateTime.Now.Year;
                DateTime fechaInicio = inicio ?? new DateTime(anioActual, 1, 1);
                DateTime fechaFin = fin ?? DateTime.Now;

                // Forzamos el último segundo del día para cerrar el rango correctamente
                fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59);

                // 2. Traer todos los usuarios/técnicos de la base de datos mapeando sus IDs principales (u.ID)
                var listaUsuariosBase = _context.Usuarios.ToList();

                // 3. SEGURO EF: Descargamos el lote anual filtrado por fecha trayendo los objetos base en memoria
                // para tener acceso dinámico a cualquier variación de nombre en la columna del Técnico
                var ticketsDelRango = _context.Tickets
                    .Where(t => t.FechaCreacion.HasValue &&
                                t.FechaCreacion.Value >= fechaInicio &&
                                t.FechaCreacion.Value <= fechaFin)
                    .ToList(); // Evaluamos de forma segura en RAM

                // 4. Inicializar listas para almacenar la información final estructurada
                var nombresTecnicos = new List<string>();
                var totalesActivos = new List<int>();
                var totalesCerrados = new List<int>();

                // 5. Procesar los tickets cruzando las propiedades de forma dinámica en memoria RAM
                foreach (var usuario in listaUsuariosBase)
                {
                    int activos = 0;
                    int cerrados = 0;

                    foreach (var ticket in ticketsDelRango)
                    {
                        int? tecnicoAsignadoId = null;

                        // DETECTOR AUTOMÁTICO DE COLUMNAS (Evita tronar por variaciones de minúsculas/mayúsculas)
                        var propiedades = ticket.GetType().GetProperties();
                        var propTecnico = propiedades.FirstOrDefault(p => p.Name.Equals("UsuarioAsignadoId", StringComparison.OrdinalIgnoreCase) ||
                                                                          p.Name.Equals("UsuarioAsignadoID", StringComparison.OrdinalIgnoreCase) ||
                                                                          p.Name.Equals("IdUsuarioAsignado", StringComparison.OrdinalIgnoreCase));

                        if (propTecnico != null)
                        {
                            var valor = propTecnico.GetValue(ticket);
                            if (valor != null) tecnicoAsignadoId = Convert.ToInt32(valor);
                        }

                        // Si el ticket le pertenece a este usuario, evaluamos su estado actual
                        if (tecnicoAsignadoId.HasValue && tecnicoAsignadoId.Value == usuario.ID)
                        {
                            if (ticket.Estado == "Resuelto")
                            {
                                cerrados++;
                            }
                            else if (ticket.Estado != "Cancelado")
                            {
                                activos++;
                            }
                        }
                    }

                    // Solo incluimos al técnico en la gráfica si tuvo actividad en este rango de fechas
                    if (activos > 0 || cerrados > 0)
                    {
                        nombresTecnicos.Add(usuario.Nombre ?? $"Usuario ID: {usuario.ID}");
                        totalesActivos.Add(activos);
                        totalesCerrados.Add(cerrados);
                    }
                }

                // 6. Contabilizar de forma dinámica los tickets que se quedaron "Sin Asignar" en este periodo
                int activosSinAsignar = 0;
                foreach (var ticket in ticketsDelRango)
                {
                    int? tecnicoAsignadoId = null;
                    var propiedades = ticket.GetType().GetProperties();
                    var propTecnico = propiedades.FirstOrDefault(p => p.Name.Equals("UsuarioAsignadoId", StringComparison.OrdinalIgnoreCase) ||
                                                                      p.Name.Equals("UsuarioAsignadoID", StringComparison.OrdinalIgnoreCase) ||
                                                                      p.Name.Equals("IdUsuarioAsignado", StringComparison.OrdinalIgnoreCase));

                    if (propTecnico != null)
                    {
                        var valor = propTecnico.GetValue(ticket);
                        if (valor != null) tecnicoAsignadoId = Convert.ToInt32(valor);
                    }

                    if (!tecnicoAsignadoId.HasValue && ticket.Estado != "Resuelto" && ticket.Estado != "Cancelado")
                    {
                        activosSinAsignar++;
                    }
                }

                if (activosSinAsignar > 0)
                {
                    nombresTecnicos.Add("Sin Asignar");
                    totalesActivos.Add(activosSinAsignar);
                    totalesCerrados.Add(0);
                }

                // 7. Retornar las listas sincronizadas por posición a Chart.js
                return Ok(new
                {
                    tecnicos = nombresTecnicos,
                    activos = totalesActivos,
                    cerrados = totalesCerrados
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerTicketsPorTecnico: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ObtenerTiemposPromedio(DateTime? inicio, DateTime? fin)
        {
            try
            {
                // 1. Configurar rango de fechas dinámico (Año actual completo por defecto)
                int anioActual = DateTime.Now.Year;
                DateTime fechaInicio = inicio ?? new DateTime(anioActual, 1, 1);
                DateTime fechaFin = fin ?? DateTime.Now;

                // Forzamos el último segundo del día seleccionado para no perder registros de la jornada
                fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59);

                // Inicializamos arreglos limpios para los 12 meses (Ene a Dic)
                double[] promedioAtencionHoras = new double[12];
                double[] promedioSolucionDias = new double[12];

                // 2. Traer los tickets que fueron creados dentro del periodo de forma segura validadando nulos en SQL
                var ticketsDelRango = _context.Tickets
                    .Where(t => t.FechaCreacion.HasValue &&
                                t.FechaCreacion.Value >= fechaInicio &&
                                t.FechaCreacion.Value <= fechaFin)
                    .ToList(); // Evaluamos de forma segura en memoria RAM

                // 3. Agrupar y calcular la información mes por mes en memoria RAM
                for (int mes = 1; mes <= 12; mes++)
                {
                    var ticketsDelMes = ticketsDelRango
                        .Where(t => t.FechaCreacion.Value.Month == mes)
                        .ToList();

                    // --- CÁLCULO 1: TIEMPO DE ATENCIÓN (De Creación a Proceso) ---
                    var ticketsAtendidos = ticketsDelMes
                        .Where(t => t.FechaActualizacion.HasValue && t.Estado != "Nuevo" && t.Estado != "Por Atender")
                        .ToList();

                    if (ticketsAtendidos.Any())
                    {
                        double totalHoras = ticketsAtendidos.Sum(t => (t.FechaActualizacion.Value - t.FechaCreacion.Value).TotalHours);
                        promedioAtencionHoras[mes - 1] = Math.Round(totalHoras / ticketsAtendidos.Count, 1);
                    }

                    // --- CÁLCULO 2: TIEMPO DE SOLUCIÓN (De Creación a Cierre) ---
                    var ticketsResueltos = ticketsDelMes
                        .Where(t => t.FechaCierre.HasValue && t.Estado == "Resuelto")
                        .ToList();

                    if (ticketsResueltos.Any())
                    {
                        double totalDias = ticketsResueltos.Sum(t => (t.FechaCierre.Value - t.FechaCreacion.Value).TotalDays);
                        promedioSolucionDias[mes - 1] = Math.Round(totalDias / ticketsResueltos.Count, 1);
                    }
                }

                // 4. Retornar las métricas calculadas a Chart.js
                return Ok(new
                {
                    atencion = promedioAtencionHoras,
                    solucion = promedioSolucionDias
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerTiemposPromedio: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpGet]
        public IActionResult ObtenerVolumenPorEstado(DateTime? inicio, DateTime? fin)
        {
            try
            {
                // 1. Configurar rango de fechas (Año actual por defecto si los parámetros vienen vacíos)
                int anioActual = DateTime.Now.Year;
                DateTime fechaInicio = inicio ?? new DateTime(anioActual, 1, 1);
                DateTime fechaFin = fin ?? DateTime.Now;

                // Forzamos el último segundo del día seleccionado para no perder tickets del cierre de jornada
                fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59);

                // 2. Descargamos la lista filtrando por el rango de fechas dinámico a nivel SQL (Máxima velocidad)
                var ticketsFiltrados = _context.Tickets
                    .Where(t => t.FechaCreacion.HasValue &&
                                t.FechaCreacion.Value >= fechaInicio &&
                                t.FechaCreacion.Value <= fechaFin)
                    .Select(t => new { t.Estado }) // Proyección ultra ligera para ahorrar memoria RAM
                    .ToList();

                // 3. Contabilizamos el acumulado del periodo por cada fase de tu flujo
                int porAtender = ticketsFiltrados.Count(e => e.Estado == "Por Atender" || e.Estado == "Nuevo");
                int enProceso = ticketsFiltrados.Count(e => e.Estado == "En Proceso");
                int enEspera = ticketsFiltrados.Count(e => e.Estado == "En Espera");
                int resueltos = ticketsFiltrados.Count(e => e.Estado == "Resuelto");

                return Ok(new
                {
                    estados = new string[] { "Por Atender", "En Proceso", "En Espera", "Resuelto" },
                    totales = new int[] { porAtender, enProceso, enEspera, resueltos }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerVolumenPorEstado: {ex.Message}");
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }


        [HttpGet]
        public IActionResult ObtenerEfectividadResolucion(DateTime? inicio, DateTime? fin)
        {
            try
            {
                // 1. Configurar rango de fechas de forma explícita (Año actual por defecto)
                int anioActual = DateTime.Now.Year;
                DateTime fechaInicio = inicio ?? new DateTime(anioActual, 1, 1);
                DateTime fechaFin = fin ?? DateTime.Now;

                // Forzamos el último segundo del día para cerrar el rango correctamente
                fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59);

                // 2. SEGURO EF: Descargamos la lista base filtrando únicamente por estado y fecha.
                // Al quitar la proyección anónima, eliminamos el fallo de traducción de tipos TEXT/MAX.
                var ticketsResueltos = _context.Tickets
                    .Where(t => t.Estado == "Resuelto" &&
                                t.FechaCreacion.HasValue &&
                                t.FechaCreacion.Value >= fechaInicio &&
                                t.FechaCreacion.Value <= fechaFin)
                    .ToList(); // Evaluamos de forma segura en RAM

                int primeraVisita = 0;
                int segundaVisita = 0;
                int tiempoExtendido = 0;

                // 3. Clasificar cada ticket según el número de interacciones en su bitácora física
                foreach (var ticket in ticketsResueltos)
                {
                    if (string.IsNullOrEmpty(ticket.Comentarios))
                    {
                        // Si por alguna razón no tiene bitácora pero está resuelto, se considera óptimo (FCR)
                        primeraVisita++;
                        continue;
                    }

                    // Dividimos las líneas de la bitácora física en memoria RAM (Súper veloz)
                    var lineasBitacora = ticket.Comentarios
                        .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(linea => !string.IsNullOrWhiteSpace(linea))
                        .ToList();

                    int numeroInteracciones = lineasBitacora.Count;

                    // Un ticket ideal solo tiene: [Ticket Tomado] y [Ticket Resuelto] (2 líneas)
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

                // Calcular el porcentaje de FCR (First Contact Resolution) basado en este periodo
                double porcentajeFcr = totalResueltos > 0
                    ? Math.Round(((double)primeraVisita / totalResueltos) * 100, 1)
                    : 0;

                // 4. Retornar el paquete JSON limpio sincronizado con Chart.js
                return Ok(new
                {
                    total = totalResueltos,
                    primera = primeraVisita,
                    segunda = segundaVisita,
                    extendido = tiempoExtendido,
                    fcr = porcentajeFcr
                });
            }
            catch (Exception ex)
            {
                // Esto imprimirá el verdadero culpable en tu consola de salida de Visual Studio si hubiera otro problema
                System.Diagnostics.Debug.WriteLine($"Error en ObtenerEfectividadResolucion: {ex.Message}");
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }


        [HttpGet]
        public IActionResult ObtenerResueltosPorCategoriaApilada(DateTime? inicio, DateTime? fin)
        {
            try
            {
                // 1. Configurar rango de fechas dinámico (Año actual por defecto si viene vacío)
                int anioActual = DateTime.Now.Year;
                DateTime fechaInicio = inicio ?? new DateTime(anioActual, 1, 1);
                DateTime fechaFin = fin ?? DateTime.Now;

                // Forzamos el último segundo del día seleccionado para incluir todos los cierres
                fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59);

                // 2. Descargar los tickets resueltos del rango seleccionado validadando nulos a nivel SQL
                var ticketsDelRango = _context.Tickets
                    .Where(t => t.Estado == "Resuelto" &&
                                t.FechaCreacion.HasValue &&
                                t.FechaCreacion.Value >= fechaInicio &&
                                t.FechaCreacion.Value <= fechaFin)
                    .ToList(); // Evaluamos de forma segura en RAM

                // 3. Traer catálogos base de IDs a Nombres (Usando .Id en minúsculas)
                var listaCategorias = _context.Categorias.ToDictionary(c => c.Id, c => c.Nombre ?? "General");
                var listaSubcategorias = _context.Subcategorias.ToDictionary(s => s.Id, s => s.Nombre ?? "General");

                // 4. Traducir y aplanar los datos en objetos limpios en memoria RAM usando reflexión blindada
                var ticketsMapeados = ticketsDelRango.Select(t =>
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

                // 5. Obtener listas únicas de Categorías (Eje X) y Subcategorías (Bloques apilados)
                var categoriasUnicas = ticketsMapeados.Select(x => x.Categoria).Distinct().ToList();
                var subcategoriasUnicas = ticketsMapeados.Select(x => x.Subcategoria).Distinct().ToList();

                // 6. Construir los conjuntos de datos (Datasets) requeridos por Chart.js
                var datasetsFinales = new List<object>();

                foreach (var sub in subcategoriasUnicas)
                {
                    var dataValores = new List<int>();

                    foreach (var cat in categoriasUnicas)
                    {
                        // Contamos cuántos tickets pertenecen a esta combinación exacta de Categoría y Subcategoría en el rango
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


        private (DateTime inicio, DateTime fin) EvaluarRangoFechas(DateTime? inicio, DateTime? fin)
        {
            // Función auxiliar para no repetir código de rangos por cada gráfica
            int anioActual = DateTime.Now.Year;
            DateTime fechaInicio = inicio ?? new DateTime(anioActual, 1, 1);
            DateTime fechaFin = fin ?? DateTime.Now;

            // Forzamos el último segundo del día para el cierre del rango
            fechaFin = new DateTime(fechaFin.Year, fechaFin.Month, fechaFin.Day, 23, 59, 59);

            return (fechaInicio, fechaFin);
        }

        [HttpGet]
        public IActionResult ObtenerPorcentajeCumplimiento(DateTime? inicio, DateTime? fin)
        {
            // Obtiene las fechas procesadas
            var rango = EvaluarRangoFechas(inicio, fin);

            // Filtramos usando el rango unificado
            var ticketsDelRango = _context.Tickets
                .Where(t => t.FechaCreacion.HasValue &&
                            t.FechaCreacion.Value >= rango.inicio &&
                            t.FechaCreacion.Value <= rango.fin)
                .ToList();

            int totalTickets = ticketsDelRango.Count;
            int resueltos = ticketsDelRango.Count(t => t.Estado == "Resuelto");
            int pendientesYOtros = totalTickets - resueltos;

            double porcentaje = totalTickets > 0 ? Math.Round(((double)resueltos / totalTickets) * 100, 1) : 0;

            return Ok(new
            {
                total = totalTickets,
                resueltos = resueltos,
                pendientes = pendientesYOtros,
                porcentajeCumplimiento = porcentaje
            });
        }

    }
}
