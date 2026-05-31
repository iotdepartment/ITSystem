using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore; // Necesario para usar .Include()

namespace ITSystem.Controllers
{
    public class GestionarTicketsController : Controller
    {
        private readonly AppDbContext _context;

        // El constructor ahora está correctamente dentro de la clase
        public GestionarTicketsController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // Cargamos los tickets e incluimos sus tablas relacionales 
            // para que la vista muestre toda la información detallada
            var tickets = _context.Tickets
                .Include(t => t.Categoria)
                .Include(t => t.Subcategoria)
                .Include(t => t.UsuarioSolicitante)
                .Include(t => t.Area)
                .OrderByDescending(t => t.FechaCreacion) // Opcional: mostrar los más nuevos primero
                .ToList();

            return View(tickets);
        }

        [HttpGet]
        public async Task<IActionResult> GetDetallesJson(int id)
        {
            var ticket = await _context.Tickets
                .Include(t => t.Area)
                .Include(t => t.Categoria)
                .Include(t => t.Subcategoria)
                .Include(t => t.UsuarioSolicitante)
                .Include(t => t.UsuarioAsignado)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (ticket == null)
            {
                return NotFound();
            }

            return Json(new
            {
                folio = ticket.Folio,
                estado = ticket.Estado,
                categoria = ticket.Categoria?.Nombre ?? "Sin categoría",
                subcategoria = ticket.Subcategoria?.Nombre ?? "General",
                area = ticket.Area?.Nombre ?? "N/A",
                solicitante = ticket.UsuarioSolicitante?.Nombre ?? "Anónimo",
                // AGREGADO: Mandamos el número de empleado para la foto
                numeroEmpleado = ticket.UsuarioSolicitante?.NumeroEmpleado,
                asignado = ticket.UsuarioAsignado?.Nombre ?? "Sin asignar",
                descripcion = ticket.Descripcion,
                comentarios = ticket.Comentarios ?? "Sin comentarios adicionales",
                fecha = ticket.FechaCreacion?.ToString("dd/MM/yyyy hh:mm tt") ?? "N/A"
            });
        }

        [HttpPost]
        public IActionResult CambiarEstado([FromBody] CambiarEstadoRequest request)
        {
            if (request == null || request.Id <= 0 || string.IsNullOrEmpty(request.Estado))
            {
                return BadRequest("Datos de solicitud inválidos.");
            }

            var ticket = _context.Tickets.FirstOrDefault(t => t.Id == request.Id);
            if (ticket == null) return NotFound("El ticket especificado no existe.");

            // 1. ASIGNACIÓN DE ESTADO Y FECHA
            ticket.Estado = request.Estado;
            ticket.FechaActualizacion = DateTime.Now;

            // 2. CORREGIDO: REGISTRO DEL TÉCNICO DESDE LA SESIÓN DE TU LOGIN
            if (request.Estado == "En Proceso")
            {
                int? tecnicoLogueadoId = HttpContext.Session.GetInt32("UsuarioID");

                if (tecnicoLogueadoId.HasValue && tecnicoLogueadoId.Value > 0)
                {
                    ticket.UsuarioAsignadoId = tecnicoLogueadoId.Value;
                }
                else
                {
                    return Unauthorized("Su sesión ha expirado. Inicie sesión nuevamente para tomar el ticket.");
                }

                // Guardar comentario automático
                string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy");
                string notaTomado = $"[Ticket Tomado - {fechaHoy}]: Ahora en proceso";
                ticket.Comentarios = string.IsNullOrEmpty(ticket.Comentarios) ? notaTomado : $"{ticket.Comentarios}\n{notaTomado}";
            }

            // 3. MOTIVO DE ESPERA
            if (request.Estado == "En Espera" && !string.IsNullOrEmpty(request.Comentario))
            {
                string fechaFormateada = $"{DateTime.Now:dd/MM/yyyy}";
                string nuevoComentario = $"[Ticket en espera - {fechaFormateada}]: {request.Comentario.Trim()}";
                ticket.Comentarios = string.IsNullOrEmpty(ticket.Comentarios) ? nuevoComentario : $"{ticket.Comentarios}\n{nuevoComentario}";
            }

            // 4. REGISTRO AUTOMÁTICO DE CIERRE
            if (request.Estado == "Resuelto")
            {
                ticket.FechaCierre = DateTime.Now;
                string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy");
                string notaResolucion = $"[Ticket Resuelto - {fechaHoy}]: Resuelto";
                ticket.Comentarios = string.IsNullOrEmpty(ticket.Comentarios) ? notaResolucion : $"{ticket.Comentarios}\n{notaResolucion}";
            }

            _context.SaveChanges();
            return Ok();
        }

    }

    // CLASE AUXILIAR ACTUALIZADA: Recibe el parámetro opcional desde el cliente
    public class CambiarEstadoRequest
    {
        public int Id { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Comentario { get; set; } // Propiedad añadida para el motivo
    }
}
