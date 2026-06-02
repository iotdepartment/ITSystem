using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace ITSystem.Controllers
{
    public class AreasController : Controller
    {
        public readonly AppDbContext _context;
        public AreasController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var areas = _context.Areas.ToList();
            return View(areas);

        }

        [HttpPost]
        public IActionResult CreateAjax([FromBody] Areas area)
        {
            if (string.IsNullOrWhiteSpace(area.Nombre))
            {
                return Json(new { success = false, message = "El nombre es obligatorio." });
            }

            bool existe = _context.Areas
                .Any(a => a.Nombre.ToLower() == area.Nombre.ToLower());

            if (existe)
            {
                return Json(new { success = false, message = "Esta área ya existe." });
            }

            _context.Areas.Add(area);
            _context.SaveChanges();

            return Json(new
            {
                success = true,
                message = "Área creada correctamente.",
                data = new { area.ID, area.Nombre }
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken] // Buena práctica de seguridad para formularios
        public async Task<IActionResult> Delete(int id)
        {
            // 1. Busca el área en la base de datos usando el ID
            var area = await _context.Areas.FindAsync(id);

            // 2. Si no existe, devuelve un error o redirecciona
            if (area == null)
            {
                return NotFound();
            }

            try
            {
                // 3. Remueve el registro del contexto
                _context.Areas.Remove(area);

                // 4. Guarda los cambios de manera asíncrona
                await _context.SaveChangesAsync();

                // Opcional: Agregar un mensaje de éxito para el usuario (TempData)
                TempData["SuccessMessage"] = "El área se eliminó correctamente.";
            }
            catch (Exception ex)
            {
                // Opcional: Manejar errores de clave foránea (ej. si el área ya tiene tickets asociados)
                TempData["ErrorMessage"] = "No se puede eliminar el área porque contiene tickets asociados.";
            }

            // 5. Redirecciona de vuelta a la vista principal de Áreas (Index)
            return RedirectToAction(nameof(Index));
        }

        // 1. ENDPOINT API: Retorna los datos del área en formato JSON para el JavaScript del modal
        [HttpGet]
        [Route("Areas/GetArea/{id}")]
        public async Task<IActionResult> GetArea(int id)
        {
            var area = await _context.Areas.FindAsync(id);
            if (area == null)
            {
                return NotFound();
            }

            // Devolvemos solo un objeto anónimo limpio con los campos necesarios
            return Json(new { id = area.ID, nombre = area.Nombre });
        }

        // 2. ACCIÓN POST: Procesa los cambios enviados desde el formulario del modal
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Areas/Edit")]
        public async Task<IActionResult> Edit(int id, string nombre)
        {
            // Buscamos el área actual en la base de datos
            var area = await _context.Areas.FindAsync(id);

            if (area == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Actualizamos sus propiedades
                    area.Nombre = nombre;

                    _context.Areas.Update(area);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Área actualizada con éxito.";
                }
                catch (Exception)
                {
                    TempData["ErrorMessage"] = "Ocurrió un error al actualizar el área.";
                }
            }

            // Redirecciona a la lista general de áreas
            return RedirectToAction(nameof(Index));
        }

    }
}
