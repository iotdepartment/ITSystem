namespace ITSystem.Models
{
    public class Tickets
    {
        public int Id { get; set; }
        public string? Folio { get; set; }
        public string? Descripcion { get; set; }
        public int? AreaId { get; set; }
        public DateTime? FechaCreacion { get; set; }
        public DateTime? FechaActualizacion { get; set; }
        public DateTime? FechaCierre { get; set; }
        public int? PrioridadId { get; set; }
        public int? CategoriaId { get; set; }
        public int? SubcategoriaId { get; set; }
        public string? Estado { get; set; }
        public int? UsuarioSolicitanteId { get; set; }
        public int? UsuarioAsignadoId { get; set; }
        public float? SLA_vencimiento { get; set; }
        public string? Comentarios { get; set; }
        // Navigation properties
        public Categorias? Categoria { get; set; }
        public Subcategorias? Subcategoria { get; set; }
        public Areas? Area { get; set; }
        public Prioridades? Prioridad { get; set; }
        public Usuarios? UsuarioSolicitante { get; set; }
        public Usuarios? UsuarioAsignado { get; set; }
    }
}
