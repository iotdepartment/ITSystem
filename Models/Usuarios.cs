using System.ComponentModel.DataAnnotations.Schema;

namespace ITSystem.Models
{
    public class Usuarios
    {
        public int ID { get; set; }
        public string? Nombre { get; set; }
        public string? Correo { get; set; }
        public int? NumeroEmpleado { get; set; }
        public int? AreaID { get; set; }
        public string? Rol { get; set; }
        public string? PasswordHash { get; set; }

        // Ruta de la imagen (para mostrar en Index)
        [NotMapped]
        public string? Foto { get; set; }

        // Archivo subido desde el formulario (no se guarda en BD)
        [NotMapped]
        public IFormFile? FotoFile { get; set; }
    }
}