using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;

namespace ITSystem.Controllers
{
    public class GestionarTicketsController : Controller
    {
        private readonly AppDbContext _context;

        public GestionarTicketsController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var tickets = _context.Tickets
                .Include(t => t.Categoria)
                .Include(t => t.Subcategoria)
                .Include(t => t.UsuarioSolicitante)
                .Include(t => t.Area)
                .OrderByDescending(t => t.FechaCreacion)
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
                // =========================================================================
                // ADICIÓN CRÍTICA: Mandamos el ID y el Id numérico del ticket para el JS
                // =========================================================================
                id = ticket.Id,
                usuarioAsignadoId = ticket.UsuarioAsignadoId, // Llave foránea numérica para pre-seleccionar el catálogo

                folio = ticket.Folio,
                estado = ticket.Estado,
                categoria = ticket.Categoria?.Nombre ?? "Sin categoría",
                subcategoria = ticket.Subcategoria?.Nombre ?? "General",
                area = ticket.Area?.Nombre ?? "N/A",
                solicitante = ticket.UsuarioSolicitante?.Nombre ?? "Anónimo",
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

            // 4. REGISTRO DE CIERRES, FECHAS Y COMENTARIO DE SOLUCIÓN PERSONALIZADO
            if (request.Estado == "Resuelto")
            {
                ticket.FechaCierre = DateTime.Now;
                string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy");

                // Si el técnico escribió un comentario, lo guardamos con el formato oficial de cierre
                string notaResolucion = $"[Ticket Resuelto - {fechaHoy}]: {request.Comentario?.Trim()}";

                if (string.IsNullOrEmpty(ticket.Comentarios))
                {
                    ticket.Comentarios = notaResolucion;
                }
                else
                {
                    ticket.Comentarios += $"\n{notaResolucion}";
                }
            }


            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public async Task<IActionResult> LiberarTicket(int id)
        {
            try
            {
                var todosLosTickets = await _context.Tickets.ToListAsync();
                var ticket = todosLosTickets.FirstOrDefault(t =>
                {
                    var props = t.GetType().GetProperties();
                    var propIdClave = props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                                                                 p.Name.Equals("ID", StringComparison.OrdinalIgnoreCase) ||
                                                                 p.Name.Equals("IdTicket", StringComparison.OrdinalIgnoreCase));
                    if (propIdClave != null)
                    {
                        var valorId = propIdClave.GetValue(t);
                        return valorId != null && Convert.ToInt32(valorId) == id;
                    }
                    return false;
                });

                if (ticket == null)
                {
                    return NotFound(new { success = false, message = "El ticket solicitado no existe en la planta." });
                }

                // 1. Modificar el estado y la fecha de actualización del flujo
                ticket.Estado = "Nuevo";
                ticket.FechaActualizacion = DateTime.Now;

                // 2. Localizar y limpiar la columna del Especialista Asignado de forma dinámica
                var propiedades = ticket.GetType().GetProperties();
                var propTecnico = propiedades.FirstOrDefault(p => p.Name.Equals("UsuarioAsignadoId", StringComparison.OrdinalIgnoreCase) ||
                                                                  p.Name.Equals("UsuarioAsignadoID", StringComparison.OrdinalIgnoreCase) ||
                                                                  p.Name.Equals("IdUsuarioAsignado", StringComparison.OrdinalIgnoreCase));

                if (propTecnico != null)
                {
                    // Dejamos la columna en NULL para quitarle la asignación al especialista
                    propTecnico.SetValue(ticket, null);
                }

                // 3. Estampar bitácora histórica automática (FORMATO SOLICITADO E IDÉNTICO A TU EJEMPLO)
                string fechaFormateada = $"{DateTime.Now:dd/MM/yyyy}";
                string nuevoComentario = $"[Ticket Liberado - {fechaFormateada}]: El ticket fue liberado por el especialista y regresó al flujo general de soporte.";

                ticket.Comentarios = string.IsNullOrEmpty(ticket.Comentarios)
                    ? nuevoComentario
                    : $"{ticket.Comentarios}\n{nuevoComentario}";

                // 4. Guardar en la Base de Datos y retornar éxito al JavaScript del modal
                _context.Update(ticket);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Ticket liberado y regresado a la bandeja con éxito." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = $"Error interno en el servidor de planta: {ex.Message}" });
            }
        }

        // 1. ENDPOINT PARA LLENAR EL SELECTOR DEL ADMINISTRADOR
        [HttpGet]
        public IActionResult ObtenerCatalogoStaff()
        {
            var staffTI = _context.Usuarios
                .Where(u => !string.IsNullOrEmpty(u.Rol) && (u.Rol == "Tecnico" || u.Rol == "Admin"))
                .OrderBy(u => u.Nombre)
                .Select(u => new
                {
                    id = u.ID,
                    nombre = u.Nombre ?? "Sin Nombre",
                    rol = u.Rol
                })
                .ToList();

            return Ok(staffTI);
        }

        // 2. ENDPOINT EXCLUSIVO PARA ASIGNAR / REASIGNAR DESDE EL CUERPO DEL MODAL
        [HttpPost]
        public async Task<IActionResult> AsignarOReasignarTicket(int ticketId, int? tecnicoId)
        {
            try
            {
                // Validación estricta de privilegios en el servidor
                string rolSesion = HttpContext.Session.GetString("UsuarioRol") ?? "";
                string administradorNombre = HttpContext.Session.GetString("UsuarioNombre") ?? "El Administrador";

                if (!rolSesion.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                {
                    return Unauthorized(new { success = false, message = "Acceso denegado. Solo administradores pueden gestionar asignaciones." });
                }

                // Descarga segura del ticket (Inmune a variaciones de ID/Id en tu modelo)
                var todosLosTickets = await _context.Tickets.ToListAsync();
                var ticket = todosLosTickets.FirstOrDefault(t =>
                {
                    var props = t.GetType().GetProperties();
                    var propId = props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("ID", StringComparison.OrdinalIgnoreCase));
                    return propId != null && Convert.ToInt32(propId.GetValue(t)) == ticketId;
                });

                if (ticket == null) return NotFound(new { success = false, message = "Ticket no localizado." });

                string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy hh:mm tt");
                string notaHistorial = "";

                // Capturar propiedades dinámicamente con reflexión para su actualización
                var propTecnicoId = ticket.GetType().GetProperties().FirstOrDefault(p => p.Name.Equals("UsuarioAsignadoId", StringComparison.OrdinalIgnoreCase) || p.Name.Equals("UsuarioAsignadoID", StringComparison.OrdinalIgnoreCase));

                // CASO A: Se seleccionó quitar la asignación o dejarlo vacío
                if (!tecnicoId.HasValue || tecnicoId.Value <= 0)
                {
                    ticket.Estado = "Nuevo";
                    if (propTecnicoId != null) propTecnicoId.SetValue(ticket, null);
                    notaHistorial = $"[Modificación Asignación - {fechaHoy}]: {administradorNombre} removió al especialista asignado. El ticket regresa a estado Nuevo.";
                }
                // CASO B: Se seleccionó un nuevo técnico (Asignación inicial o Cambio de dueño)
                else
                {
                    var nuevoTecnico = await _context.Usuarios.FirstOrDefaultAsync(u => u.ID == tecnicoId.Value);
                    if (nuevoTecnico == null) return NotFound(new { success = false, message = "El especialista seleccionado no existe." });

                    int? antiguoTecnicoId = null;
                    if (propTecnicoId != null) antiguoTecnicoId = (int?)propTecnicoId.GetValue(ticket);

                    // Cambiamos el estado a En Proceso de forma automática
                    ticket.Estado = "En Proceso";
                    if (propTecnicoId != null) propTecnicoId.SetValue(ticket, tecnicoId.Value);

                    if (!antiguoTecnicoId.HasValue)
                    {
                        // Asignación Inicial
                        notaHistorial = $"[Asignación Inicial - {fechaHoy}]: {administradorNombre} asignó el ticket al especialista {nuevoTecnico.Nombre}.";
                    }
                    else if (antiguoTecnicoId.Value != tecnicoId.Value)
                    {
                        // Reasignación (Cambio de dueño en caliente)
                        notaHistorial = $"[Reasignación - {fechaHoy}]: {administradorNombre} cambió la asignación del ticket al especialista {nuevoTecnico.Nombre}.";
                    }

                    // Notificación por correo al nuevo dueño asignado (Opcional - Puerto 25 local Planta)
                    if (!string.IsNullOrEmpty(nuevoTecnico.Correo))
                    {
                        _ = EnviarCorreoAlertaAsignacion(nuevoTecnico.Correo, ticket.Folio, administradorNombre);
                    }
                }

                // Estampamos el comentario con tu formato de operador ternario e interpolación nativa
                if (!string.IsNullOrEmpty(notaHistorial))
                {
                    ticket.Comentarios = string.IsNullOrEmpty(ticket.Comentarios) ? notaHistorial : $"{ticket.Comentarios}\n{notaHistorial}";
                }

                _context.Update(ticket);
                await _context.SaveChangesAsync();

                return Ok(new { success = true, message = "Asignación actualizada correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
        }

        // Método de soporte para despacho de correos
        private async Task EnviarCorreoAlertaAsignacion(string correoDestino, string folio, string adminNombre)
        {
            try
            {
                using (MailMessage mensaje = new MailMessage())
                {
                    mensaje.From = new MailAddress("extrudersys@outlook.com", "Mesa de Control TI");
                    mensaje.To.Add(correoDestino);
                    mensaje.Subject = $"📋 ASIGNACIÓN DE TICKET TI - Folio: {folio}";
                    mensaje.IsBodyHtml = true;
                    mensaje.Body = $"<h3>Orden de Trabajo Asignada</h3><p>El administrador <strong>{adminNombre}</strong> te ha asignado el ticket con folio <strong>{folio}</strong>. Por favor, ingresa al sistema para atenderlo.</p>";
                    using (SmtpClient smtp = new SmtpClient("kysmtp.tggroup.local", 25))
                    {
                        smtp.Credentials = new NetworkCredential("extrudersys@outlook.com", "01TG-ExtSys2024");
                        smtp.EnableSsl = false;
                        await smtp.SendMailAsync(mensaje);
                    }
                }
            }
            catch { /* Silencioso en red local */ }
        }




        [HttpPost]
        public IActionResult AgregarNotaSeguimiento([FromBody] AgregarNotaRequest request)
        {
            if (request == null || request.Id <= 0 || string.IsNullOrEmpty(request.Comentario))
            {
                return BadRequest("Datos de solicitud inválidos o comentario vacío.");
            }

            var ticket = _context.Tickets.FirstOrDefault(t => t.Id == request.Id);
            if (ticket == null) return NotFound("El ticket especificado no existe.");

            // 1. Registrar únicamente la fecha de actualización y la nota
            ticket.FechaActualizacion = DateTime.Now;

            string fechaHoy = DateTime.Now.ToString("dd/MM/yyyy");
            string notaInterna = $"[Nota de Seguimiento - {fechaHoy}]: {request.Comentario.Trim()}";

            if (string.IsNullOrEmpty(ticket.Comentarios))
            {
                ticket.Comentarios = notaInterna;
            }
            else
            {
                ticket.Comentarios += $"\n{notaInterna}";
            }

            _context.SaveChanges();
            return Ok(new { success = true, message = "Nota agregada correctamente." });
        }

        // Modelo de datos para este endpoint (puedes colocarlo al final de tu archivo)
        public class AgregarNotaRequest
        {
            public int Id { get; set; }
            public string Comentario { get; set; }
        }


    }

    public class CambiarEstadoRequest
    {
        public int Id { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? Comentario { get; set; }
    }


}
