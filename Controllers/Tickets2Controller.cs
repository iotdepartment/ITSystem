using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Crear(Tickets ticket)
        {
            if (ModelState.IsValid)
            {
                // Configurar valores por defecto para un ticket nuevo
                ticket.FechaCreacion = DateTime.Now;
                ticket.FechaActualizacion = DateTime.Now;
                ticket.Estado = "Nuevo"; // Ajusta según tus estados en la base de datos

                string fechaFormato = DateTime.Now.ToString("yyyyMMdd");
                int numeroAleatorio = new Random().Next(1000, 9999);
                ticket.Folio = $"TK-{fechaFormato}-{numeroAleatorio}";

                _context.Add(ticket);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            // SI EL MODELO NO ES VÁLIDO: Volvemos a llenar los catálogos exactamente igual que en el Index
            var listaAreas = await _context.Areas.OrderBy(a => a.Nombre).ToListAsync();
            ViewBag.Areas = new SelectList(listaAreas, "ID", "Nombre"); // O "Id" según corresponda

            var listaCategorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();
            ViewBag.Categorias = new SelectList(listaCategorias, "Id", "Nombre");

            var listaUsuarios = await _context.Usuarios.OrderBy(u => u.Nombre).ToListAsync();
            ViewBag.Usuarios = new SelectList(listaUsuarios, "ID", "Nombre"); // O "Id" según corresponda

            // Recargar el listado principal para que la vista Index no falle al renderizar la tabla/cards
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

