using ITSystem.Models; // Asegúrate de importar el namespace de tus modelos
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ITSystem.Controllers
{
    public class LoginController : Controller
    {
        private readonly AppDbContext _context; // Reemplaza por el nombre real de tu DbContext

        public LoginController(AppDbContext context)
        {
            _context = context;
        }

        // Vista de Login (Muestra el formulario)
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        // Acción que procesa el formulario mediante AJAX
        [HttpPost]
        public async Task<IActionResult> Login([FromForm] int NumeroEmpleado, [FromForm] string Password)
        {
            // 1. Validar campos vacíos
            if (NumeroEmpleado <= 0 || string.IsNullOrWhiteSpace(Password))
            {
                return Json(new { success = false, message = "Por favor, complete todos los campos." });
            }

            try
            {
                // 2. Buscar al usuario por Número de Empleado
                var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.NumeroEmpleado == NumeroEmpleado);

                if (usuario == null)
                {
                    return Json(new { success = false, message = "El número de empleado o la contraseña son incorrectos." });
                }

                // 3. Validar si tiene contraseña en la BD
                if (string.IsNullOrEmpty(usuario.PasswordHash))
                {
                    return Json(new { success = false, message = "El usuario no tiene una contraseña configurada." });
                }

                // 4. Verificar Hash con BCrypt
                bool passwordValido = BCrypt.Net.BCrypt.Verify(Password, usuario.PasswordHash);

                if (!passwordValido)
                {
                    return Json(new { success = false, message = "El número de empleado o la contraseña son incorrectos." });
                }

                // 5. Crear variables de sesión
                HttpContext.Session.SetInt32("UsuarioID", usuario.ID);
                HttpContext.Session.SetString("UsuarioNombre", usuario.Nombre ?? "Usuario");
                HttpContext.Session.SetString("UsuarioRol", usuario.Rol ?? "Solicitante");

                // Forzamos la extracción del valor entero real usando .Value o un valor por defecto
                HttpContext.Session.SetInt32("UsuarioEmpleado", usuario.NumeroEmpleado.HasValue ? usuario.NumeroEmpleado.Value : 0);

                return Json(new { success = true });

            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error interno: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear(); // Borra todas las variables de sesión
            return RedirectToAction("Index", "Login"); // Redirecciona de vuelta al formulario
        }

    }
}
