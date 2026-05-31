using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITSystem.Controllers
{
    public class CategoriasController : Controller
    {
        private readonly AppDbContext _context;

        public CategoriasController(AppDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var categorias = _context.Categorias
                .Include(c => c.Subcategorias)
                .ToList();

            return View(categorias);
        }

        [HttpPost]
        public IActionResult AgregarCategoria(Categorias categoria)
        {
            if (ModelState.IsValid)
            {
                _context.Categorias.Add(categoria);
                _context.SaveChanges();

                // Implementa PRG (Post-Redirect-Get) para evitar reenvíos de formulario al presionar F5
                return RedirectToAction("Index");
            }

            // Si la validación falla por alguna razón, recarga la vista con los datos actuales
            var categorias = _context.Categorias
                .Include(c => c.Subcategorias)
                .ToList();

            return View("Index", categorias);
        }


        [HttpPost]
        public IActionResult AgregarSubcategoria(Subcategorias subcategoria)
        {
            if (ModelState.IsValid)
            {
                _context.Subcategorias.Add(subcategoria);
                _context.SaveChanges();

                // PRG: redirige para evitar repetir el POST
                return RedirectToAction("Index");
            }

            var categorias = _context.Categorias
                .Include(c => c.Subcategorias)
                .ToList();

            return View("Index", categorias);
        }

        // Endpoint para borrar solo una subcategoría desde el modal por AJAX
        [HttpPost]
        public IActionResult EliminarSubcategoria(int id)
        {
            var sub = _context.Subcategorias.Find(id);
            if (sub == null) return NotFound();

            _context.Subcategorias.Remove(sub);
            _context.SaveChanges();
            return Ok();
        }

        // Endpoint para actualizar el nombre de la categoría por AJAX
        [HttpPost]
        public IActionResult EditarNombre([FromBody] Categorias datos)
        {
            var categoria = _context.Categorias.Find(datos.Id);
            if (categoria == null) return NotFound();

            categoria.Nombre = datos.Nombre;
            _context.SaveChanges();
            return Ok();
        }

        [HttpPost]
        public IActionResult EditarSubcategoriaNombre([FromBody] Subcategorias datos)
        {
            var subcategoria = _context.Subcategorias.Find(datos.Id);
            if (subcategoria == null) return NotFound();

            subcategoria.Nombre = datos.Nombre;
            _context.SaveChanges();

            return Ok();
        }

        [HttpPost]
        public IActionResult ConfirmarEliminar(int id)
        {
            var categoria = _context.Categorias
                .Include(c => c.Subcategorias)
                .FirstOrDefault(c => c.Id == id);

            if (categoria == null)
            {
                return NotFound();
            }

            // Remueve la categoría de la BD (Entity Framework borra las subcategorías en cascada automáticamente)
            _context.Categorias.Remove(categoria);
            _context.SaveChanges();

            // Redirige al index para actualizar el listado de tarjetas en pantalla
            return RedirectToAction("Index");
        }

    }
}
