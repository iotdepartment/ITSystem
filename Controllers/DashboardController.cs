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


    }
}
