using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace ITSystem.Controllers
{
    public class SubcategoriasController : Controller
    {
        private readonly AppDbContext _context;

        public SubcategoriasController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(Subcategorias subcategoria)
        {
            if (ModelState.IsValid)
            {
                _context.Subcategorias.Add(subcategoria);
                _context.SaveChanges();
                return RedirectToAction("Index", new { id = subcategoria.CategoriaID });
            }
            return View(subcategoria);
        }
    }
}
