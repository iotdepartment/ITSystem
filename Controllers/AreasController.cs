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
    }
}
