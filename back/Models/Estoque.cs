using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace back.Models
{
    public class Estoque
    {
        public int Id { get; set; }
        public int EmpresaId { get; set; }
        public int ItemId { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public int Quantidade { get; set; }
        public double CustoUnitario { get; set; }
        public double PrecoVenda { get; set; }
        public double PrecoPromocional { get; set; }
        public string Documento { get; set; } = string.Empty;
        public DateTime Data { get; set; }
        public int UsuarioId { get; set; }
    }
}