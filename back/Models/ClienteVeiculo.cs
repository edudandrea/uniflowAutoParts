using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace back.Models
{
    public class ClienteVeiculo
    {
        public long Id { get; set; }

        public long ClienteId { get; set; }
        public long? VeiculoVersaoId { get; set; }

        public string? Placa { get; set; }
        public string? Chassi { get; set; }
        public string? Renavam { get; set; }

        public int? AnoFabricacao { get; set; }
        public int? AnoModelo { get; set; }

        public string? Cor { get; set; }
        public int? Quilometragem { get; set; }

        public string? Observacao { get; set; }

        public bool Ativo { get; set; } = true;

        public Cliente Cliente { get; set; } = null!;
    }
}