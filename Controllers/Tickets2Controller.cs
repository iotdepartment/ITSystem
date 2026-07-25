using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;


namespace ITSystem.Controllers
{
    public class Tickets2Controller : Controller
    {
        private readonly AppDbContext _context;
        public Tickets2Controller(AppDbContext context)
        {
            _context = context;
        }

        // GET: Tickets
        public async Task<IActionResult> Index()
        {
            // 1. Cargar Áreas
            var listaAreas = await _context.Areas.OrderBy(a => a.Nombre).ToListAsync();
            ViewBag.Areas = new SelectList(listaAreas, "ID", "Nombre");

            // 2. Cargar Categorías
            var listaCategorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();
            ViewBag.Categorias = new SelectList(listaCategorias, "Id", "Nombre");

            // 3. Cargar Usuarios (Usando "ID" en mayúsculas y "Nombre" de tu modelo)
            var listaUsuarios = await _context.Usuarios.OrderBy(u => u.Nombre).ToListAsync();
            ViewBag.Usuarios = new SelectList(listaUsuarios, "ID", "Nombre");

            // 4. MODIFICADO: Cargar listado de tickets incluyendo sus relaciones para las cards
            var tickets = await _context.Tickets
                .Include(t => t.Area)
                .Include(t => t.Categoria)
                .Include(t => t.Subcategoria)
                .Include(t => t.UsuarioSolicitante)
                .OrderByDescending(t => t.FechaCreacion) // Muestra los más nuevos primero
                .ToListAsync();

            return View(tickets);
        }

        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public async Task<IActionResult> Crear(Tickets ticket)
        //{
        //    if (ModelState.IsValid)
        //    {
        //        // Configurar valores por defecto para un ticket nuevo
        //        ticket.FechaCreacion = DateTime.Now;
        //        ticket.FechaActualizacion = DateTime.Now;
        //        ticket.Estado = "Nuevo"; // Ajusta según tus estados en la base de datos

        //        string fechaFormato = DateTime.Now.ToString("yyyyMMdd");
        //        int numeroAleatorio = new Random().Next(1000, 9999);
        //        ticket.Folio = $"TK-{fechaFormato}-{numeroAleatorio}";

        //        _context.Add(ticket);
        //        await _context.SaveChangesAsync();

        //        return RedirectToAction(nameof(Index));
        //    }

        //    // SI EL MODELO NO ES VÁLIDO: Volvemos a llenar los catálogos exactamente igual que en el Index
        //    var listaAreas = await _context.Areas.OrderBy(a => a.Nombre).ToListAsync();
        //    ViewBag.Areas = new SelectList(listaAreas, "ID", "Nombre"); // O "Id" según corresponda

        //    var listaCategorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();
        //    ViewBag.Categorias = new SelectList(listaCategorias, "Id", "Nombre");

        //    var listaUsuarios = await _context.Usuarios.OrderBy(u => u.Nombre).ToListAsync();
        //    ViewBag.Usuarios = new SelectList(listaUsuarios, "ID", "Nombre"); // O "Id" según corresponda

        //    // Recargar el listado principal para que la vista Index no falle al renderizar la tabla/cards
        //    var tickets = await _context.Tickets
        //        .Include(t => t.Area)
        //        .Include(t => t.Categoria)
        //        .Include(t => t.Subcategoria)
        //        .Include(t => t.UsuarioSolicitante)
        //        .OrderByDescending(t => t.FechaCreacion)
        //        .ToListAsync();

        //    return View("Index", tickets);
        //}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Tickets ticket)
        {
            if (ModelState.IsValid)
            {
                // Configurar valores por defecto para un ticket nuevo (Tus reglas originales)
                ticket.FechaCreacion = DateTime.Now;
                ticket.FechaActualizacion = DateTime.Now;
                ticket.Estado = "Nuevo";

                string fechaFormato = DateTime.Now.ToString("yyyyMMdd");
                int numeroAleatorio = new Random().Next(1000, 9999);
                ticket.Folio = $"TK-{fechaFormato}-{numeroAleatorio}";

                _context.Add(ticket);
                await _context.SaveChangesAsync();

                // =========================================================================
                // PROCESO DE ALERTA DE CORREO ELECTRONICO (TECNICOS Y ADMINS)
                // =========================================================================
                try
                {
                    // 1. Extraer de la base de datos únicamente los correos de los roles solicitados
                    var correosStaff = _context.Usuarios
                        .Where(u => (!string.IsNullOrEmpty(u.Rol) && (u.Rol == "Tecnico" || u.Rol == "Admin")) && !string.IsNullOrEmpty(u.Correo))
                        .Select(u => u.Correo)
                        .Distinct()
                        .ToList();

                    if (correosStaff.Any())
                    {
                        // 2. Traer nombres reales de referencias para la plantilla HTML del correo
                        string nombreSolicitante = _context.Usuarios.FirstOrDefault(u => u.ID == ticket.UsuarioSolicitanteId)?.Nombre ?? "No Especificado";
                        string nombreCategoria = _context.Categorias.FirstOrDefault(c => c.Id == ticket.CategoriaId)?.Nombre ?? "General";

                        // 3. Credenciales del Servidor SMTP de tu Planta (Integradas)
                        string servidorSmtp = "kysmtp.tggroup.local";
                        int puertoSmtp = 25;
                        string correoEmisor = "extrudersys@outlook.com";
                        string passwordEmisor = "01TG-ExtSys2024";

                        using (MailMessage mensaje = new MailMessage())
                        {
                            mensaje.From = new MailAddress(correoEmisor, "IT System");
                            mensaje.Subject = $"🚨 NUEVO TICKET GENERADO - Folio: {ticket.Folio}";
                            mensaje.IsBodyHtml = true;

                            // Enviamos como Copia Oculta (Bcc) para despachar un solo paquete masivo
                            foreach (var correo in correosStaff)
                            {
                                mensaje.Bcc.Add(correo);
                            }

                            // 4. Estructura visual de la plantilla del correo con Bootstrap embebido
                            mensaje.Body = $@"
                                <div style='font-family: system-ui, -apple-system, sans-serif; max-width: 600px; margin: 0 auto; border: 1px solid #e2e8f0; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.1);'>
                                    <div style='background-color: #0d6efd; color: #ffffff; padding: 20px; text-align: center;'>
                                        <h2 style='margin: 0; font-weight: bold;'>⚠️ NUEVO TICKET GENERADO</h2>
                                    </div>
                                    <div style='padding: 24px; background-color: #ffffff; color: #334155;'>
                                        <p style='margin-top: 0;'>Estimado equipo de soporte,</p>
                                        <p>Se les notifica que un nuevo ticket requiere de su atención. A continuación se presentan los detalles del reporte técnico:</p>
                            
                                        <table style='width: 100%; border-collapse: collapse; margin: 20px 0;'>
                                            <tr style='background-color: #f8fafc;'>
                                                <td style='padding: 10px; font-weight: bold; width: 35%; border-bottom: 1px solid #e2e8f0;'>Folio Asignado:</td>
                                                <td style='padding: 10px; border-bottom: 1px solid #e2e8f0; color: #0d6efd; font-weight: bold;'>{ticket.Folio}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 10px; font-weight: bold; border-bottom: 1px solid #e2e8f0;'>Solicitante:</td>
                                                <td style='padding: 10px; border-bottom: 1px solid #e2e8f0;'>{nombreSolicitante}</td>
                                            </tr>
                                            <tr style='background-color: #f8fafc;'>
                                                <td style='padding: 10px; font-weight: bold; border-bottom: 1px solid #e2e8f0;'>Categoría Servicio:</td>
                                                <td style='padding: 10px; border-bottom: 1px solid #e2e8f0;'>{nombreCategoria}</td>
                                            </tr>
                                            <tr>
                                                <td style='padding: 10px; font-weight: bold; border-bottom: 1px solid #e2e8f0;'>Fecha de Apertura:</td>
                                                <td style='padding: 10px; border-bottom: 1px solid #e2e8f0;'>{(ticket.FechaCreacion.HasValue ? ticket.FechaCreacion.Value.ToString("dd/MM/yyyy hh:mm tt") : DateTime.Now.ToString("dd/MM/yyyy hh:mm tt"))}</td>
                                            </tr>
                                        </table>

                                        <div style='background-color: #fffbeb; border-left: 4px solid #f59e0b; padding: 15px; border-radius: 4px; margin-bottom: 25px;'>
                                            <strong style='color: #b45309; display: block; margin-bottom: 5px;'>Descripción del Problema:</strong>
                                            <span style='color: #451a03; line-height: 1.5;'>{ticket.Descripcion}</span>
                                        </div>

                                        <div style='text-align: center; margin-top: 30px;'>
                                            <a href='http://10.195.250.100:22000/Login' style='background-color: #198754; color: #ffffff; text-decoration: none; padding: 12px 25px; font-weight: bold; border-radius: 50px; display: inline-block;'>
                                                🚀 Atender e Ir al Ticket
                                            </a>
                                        </div>
                                    </div>
                                    <div style='background-color: #f1f5f9; color: #64748b; font-size: 12px; padding: 15px; text-align: center; border-top: 1px solid #e2e8f0;'>
                                        Este es un correo automático generado por IT System. Por favor no responder directamente.
                                    </div>
                                </div>";

                            // 5. Despacho asíncrono final
                            using (SmtpClient smtp = new SmtpClient(servidorSmtp, puertoSmtp))
                            {
                                smtp.Credentials = new NetworkCredential(correoEmisor, passwordEmisor);
                                // AJUSTE CLAVE LOCAL: Puerto 25 en redes locales comúnmente opera sin SSL/TLS rígido
                                smtp.EnableSsl = false;
                                await smtp.SendMailAsync(mensaje);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Bloque preventivo: Si el SMTP local está ocupado, el ticket SÍ se crea
                    System.Diagnostics.Debug.WriteLine($"Error al enviar correos de notificación: {ex.Message}");
                }

                return RedirectToAction(nameof(Index));
            }

            // =========================================================================
            // SI EL MODELO NO ES VÁLIDO: (Mantiene intacta tu lógica de recarga original)
            // =========================================================================
            var listaAreas = await _context.Areas.OrderBy(a => a.Nombre).ToListAsync();
            ViewBag.Areas = new SelectList(listaAreas, "ID", "Nombre");

            var listaCategorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();
            ViewBag.Categorias = new SelectList(listaCategorias, "Id", "Nombre");

            var listaUsuarios = await _context.Usuarios.OrderBy(u => u.Nombre).ToListAsync();
            ViewBag.Usuarios = new SelectList(listaUsuarios, "ID", "Nombre");

            var tickets = await _context.Tickets
                .Include(t => t.Area)
                .Include(t => t.Categoria)
                .Include(t => t.Subcategoria)
                .Include(t => t.UsuarioSolicitante)
                .OrderByDescending(t => t.FechaCreacion)
                .ToListAsync();

            return View("Index", tickets);
        }



        [HttpGet]
        public async Task<IActionResult> BuscarUsuarioPorNumero(string numeroEmpleado)
        {
            if (string.IsNullOrEmpty(numeroEmpleado) || !int.TryParse(numeroEmpleado, out int numeroConvertido))
            {
                return BadRequest(new { mensaje = "Número no válido" });
            }

            // CORREGIDO: Buscamos por la columna 'NumeroEmpleado' usando el valor convertido
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.NumeroEmpleado == numeroConvertido);

            if (usuario == null)
            {
                return NotFound(new { mensaje = "Empleado no encontrado" });
            }

            // Devolvemos el 'ID' interno de la BD para que se guarde en la tabla de Tickets
            return Json(new
            {
                id = usuario.ID, // O 'usuario.Id' según tengas la clave primaria en tu clase Usuarios
                nombre = usuario.Nombre
            });
        }


        [HttpGet]
        public async Task<JsonResult> GetSubcategorias(int categoriaId)
        {
            var subcategorias = await _context.Subcategorias
                .Where(s => s.CategoriaID == categoriaId) // Usamos CategoriaID tal como está en tu modelo
                .OrderBy(s => s.Nombre)
                .Select(s => new
                {
                    id = s.Id,        // Tu propiedad 'Id'
                    nombre = s.Nombre // Tu propiedad 'Nombre'
                })
                .ToListAsync();

            return Json(subcategorias);
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
                fecha = ticket.FechaCreacion?.ToString("dd/MM/yyyy") ?? "N/A"
            });
        }

    }
}

