namespace ITSystem.Models
{
    public class Categorias
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public ICollection<Subcategorias>? Subcategorias { get; set; }
    }
}
