using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


namespace ITSystem.Controllers
{
    public class TicketsController : Controller
    {
        private readonly AppDbContext _context;
        public TicketsController(AppDbContext context)
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear([Bind("Descripcion,AreaId,CategoriaId,SubcategoriaId")] Tickets ticket)
        {
            // 1. LEER EL ID DIRECTAMENTE DESDE LA SESIÓN QUE CREÓ TU LOGIN
            int? usuarioLogueadoId = HttpContext.Session.GetInt32("UsuarioID");

            if (usuarioLogueadoId.HasValue && usuarioLogueadoId.Value > 0)
            {
                ticket.UsuarioSolicitanteId = usuarioLogueadoId.Value;
            }
            else
            {
                // Si no hay sesión activa, redirigir al login o mandar error en lugar de asignar el ID 1
                TempData["ErrorMessage"] = "Su sesión ha expirado. Por favor, inicie sesión nuevamente.";
                return RedirectToAction("Login", "Index"); // Ajusta el nombre de tu controlador de Login si varía
            }

            // Remover validaciones automáticas de propiedades de navegación para evitar conflictos en el ModelState
            ModelState.Remove("UsuarioSolicitanteId");
            ModelState.Remove("UsuarioSolicitante");

            if (ModelState.IsValid)
            {
                try
                {
                    // 2. Configuración de auditoría y estados por defecto
                    ticket.FechaCreacion = DateTime.Now;
                    ticket.FechaActualizacion = DateTime.Now;
                    ticket.Estado = "Nuevo";

                    // 3. Generación automática de Folio único
                    string fechaFormato = DateTime.Now.ToString("yyyyMMdd");
                    int numeroAleatorio = new Random().Next(1000, 9999);
                    ticket.Folio = $"TK-{fechaFormato}-{numeroAleatorio}";

                    // 4. Guardar de manera asíncrona en la base de datos
                    _context.Add(ticket);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = $"Ticket {ticket.Folio} creado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "No se pudo guardar el ticket. Intente de nuevo más tarde.");
                }
            }

            TempData["ErrorMessage"] = "Hubo un error al validar los datos del ticket.";
            return RedirectToAction(nameof(Index));
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
                fecha = ticket.FechaCreacion?.ToString("dd/MM/yyyy hh:mm tt") ?? "N/A"
            });
        }

    }
}

