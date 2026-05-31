using ITSystem.Models;
using Microsoft.AspNetCore.Mvc;

namespace ITSystem.Controllers
{
    public class UsuariosController : Controller
    {
        public readonly AppDbContext _context;
        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            ViewBag.Areas = _context.Areas.ToList();

            // Ordenamos por NumeroEmpleado
            var usuarios = _context.Usuarios
                                   .OrderBy(u => u.NumeroEmpleado)
                                   .ToList();

            foreach (var u in usuarios)
            {
                string basePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                string png = Path.Combine(basePath, $"{u.NumeroEmpleado}.png");
                string jpg = Path.Combine(basePath, $"{u.NumeroEmpleado}.jpg");
                string jpeg = Path.Combine(basePath, $"{u.NumeroEmpleado}.jpeg");

                if (System.IO.File.Exists(png))
                    u.Foto = $"/Images/{u.NumeroEmpleado}.png";
                else if (System.IO.File.Exists(jpg))
                    u.Foto = $"/Images/{u.NumeroEmpleado}.jpg";
                else if (System.IO.File.Exists(jpeg))
                    u.Foto = $"/Images/{u.NumeroEmpleado}.jpeg";
                else
                    u.Foto = "/Images/default.png";
            }

            return View(usuarios);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromForm] Usuarios usuario, [FromForm] string Password)
        {
            // Elimina errores de validación innecesarios para el binding
            ModelState.Remove(nameof(usuario.FotoFile));
            ModelState.Remove(nameof(usuario.PasswordHash));

            // Validar manualmente que la contraseña no venga vacía
            if (string.IsNullOrWhiteSpace(Password))
            {
                return Json(new { success = false, message = "La contraseña es requerida." });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // 1. Encriptar la contraseña usando BCrypt
                    usuario.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);

                    // 2. Procesar la foto
                    if (usuario.FotoFile != null && usuario.FotoFile.Length > 0)
                    {
                        var fileName = usuario.NumeroEmpleado + Path.GetExtension(usuario.FotoFile.FileName);
                        var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images", fileName);

                        using (var stream = new FileStream(path, FileMode.Create))
                        {
                            await usuario.FotoFile.CopyToAsync(stream);
                        }

                        usuario.Foto = "/Images/" + fileName;
                    }
                    else
                    {
                        usuario.Foto = "/Images/default.png";
                    }

                    _context.Add(usuario);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = ex.Message });
                }
            }

            return Json(new { success = false, message = "Datos del formulario inválidos." });
        }


        [HttpGet]
        public IActionResult GetById(int id)
        {
            var usuario = _context.Usuarios.FirstOrDefault(u => u.ID == id);
            if (usuario == null) return NotFound();

            return Json(new
            {
                id = usuario.ID,
                nombre = usuario.Nombre,
                correo = usuario.Correo,
                numeroEmpleado = usuario.NumeroEmpleado,
                areaID = usuario.AreaID,
                rol = usuario.Rol
            });
        }

        [HttpPost]
        public async Task<IActionResult> Edit([FromForm] Usuarios usuarioModificado, [FromForm] string? Password)
        {
            // Limpieza de validaciones innecesarias del modelo base
            ModelState.Remove(nameof(usuarioModificado.FotoFile));
            ModelState.Remove(nameof(usuarioModificado.PasswordHash));

            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "Datos del formulario inválidos." });
            }

            try
            {
                // 1. Buscar el usuario actual en la base de datos
                var usuarioDB = await _context.Usuarios.FindAsync(usuarioModificado.ID);
                if (usuarioDB == null)
                {
                    return Json(new { success = false, message = "Usuario no encontrado." });
                }

                // 2. Actualizar campos de texto básicos
                usuarioDB.Nombre = usuarioModificado.Nombre;
                usuarioDB.Correo = usuarioModificado.Correo;
                usuarioDB.NumeroEmpleado = usuarioModificado.NumeroEmpleado;
                usuarioDB.AreaID = usuarioModificado.AreaID;
                usuarioDB.Rol = usuarioModificado.Rol;

                // 3. Procesar Contraseña SOLO si el administrador escribió una nueva
                if (!string.IsNullOrWhiteSpace(Password))
                {
                    usuarioDB.PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password);
                }

                // 4. Procesar Foto nueva si se seleccionó un archivo
                if (usuarioModificado.FotoFile != null && usuarioModificado.FotoFile.Length > 0)
                {
                    var fileName = usuarioModificado.NumeroEmpleado + Path.GetExtension(usuarioModificado.FotoFile.FileName);
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Images", fileName);

                    using (var stream = new FileStream(path, FileMode.Create))
                    {
                        await usuarioModificado.FotoFile.CopyToAsync(stream);
                    }

                    usuarioDB.Foto = "/Images/" + fileName;
                }

                // 5. Guardar los cambios en la base de datos
                _context.Update(usuarioDB);
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno: " + ex.Message });
            }
        }


        [HttpPost]
        public IActionResult Delete(int id)
        {
            var usuario = _context.Usuarios.FirstOrDefault(u => u.ID == id);
            if (usuario == null) return NotFound();

            _context.Usuarios.Remove(usuario);
            _context.SaveChanges();

            return Json(new { success = true });
        }
    }
}
