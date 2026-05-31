namespace ITSystem.Models
{
    public class Subcategorias
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int CategoriaID { get; set; }
        public Categorias? Categoria { get; set; }
    }
}
