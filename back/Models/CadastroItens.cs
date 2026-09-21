using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace back.Models
{
    public class CadastroItens
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public int SKU { get; set; }
        public int Codintern { get; set; }
        public int Codfabricante { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public long CategoriaId { get; set; }
        public Categoria? Categoria { get; set; }
        public long MarcaId { get; set; }
        public Marca? Marca { get; set; }
        public int FabricanteId { get; set; }
        public double Custo { get; set; }
        public double Preco { get; set; }
        public int EstoqueMinimo { get; set; }
        public int EstoqueMaximo { get; set; }
        public int NCM { get; set; }
        public int CEST { get; set; }
        public string OrigemMercadoria { get; set; } = string.Empty;
        public bool Ativo { get; set; }
        
        
    }
}
