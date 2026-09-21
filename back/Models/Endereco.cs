using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace back.Models
{
    public class Endereco
    {
        public long Id { get; set; }

    public long ClienteId { get; set; }

    public TipoEndereco Tipo { get; set; }

    public string Cep { get; set; } = null!;
    public string Logradouro { get; set; } = null!;
    public string Numero { get; set; } = null!;
    public string? Complemento { get; set; }
    public string Bairro { get; set; } = null!;

    public string CodigoIbgeMunicipio { get; set; } = null!;
    public string Municipio { get; set; } = null!;
    public string Uf { get; set; } = null!;
    public string Pais { get; set; } = "Brasil";

    public bool Principal { get; set; }

    public Cliente Cliente { get; set; } = null!;
    }
}