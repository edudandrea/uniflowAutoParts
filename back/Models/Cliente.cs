using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace back.Models
{
    public class Cliente
    {
        public long Id { get; set; }

    // SaaS / Empresa proprietária do cadastro
    public long TenantId { get; set; }

    // Identificação
    public TipoPessoa TipoPessoa { get; set; } 

    public string NomeRazaoSocial { get; set; } = null!;
    public string? NomeFantasia { get; set; }

    public string CpfCnpj { get; set; } = null!;
    public string? Rg { get; set; }
    public string? OrgaoEmissor { get; set; }
    public string? EstadoCivil { get; set; }
    public DateOnly? DataNascimento { get; set; }
    public string? Profissao { get; set; }
    public string? Cnh { get; set; }
    public string? InscricaoEstadual { get; set; }
    public string? InscricaoMunicipal { get; set; }
    public RelacionamentoCadastro Relacionamento { get; set; } = RelacionamentoCadastro.Cliente;

    // Fiscal
    public IndicadorIe IndicadorIe { get; set; }
    public bool ConsumidorFinal { get; set; }

    // Contato principal
    public string? Email { get; set; }
    public string? Telefone { get; set; }
    public string? Celular { get; set; }

    // Comercial
    public decimal? LimiteCredito { get; set; }
    public DateOnly? ClienteDesde { get; set; }
    public string? Origem { get; set; }
    public string? VendedorPadrao { get; set; }
    public string? Observacao { get; set; }

    // Controle
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; }
    public DateTime? AtualizadoEm { get; set; }

    // Relacionamentos
    public long? CadastroEnderecoId { get; set; }
    public CadastroEndereco? CadastroEndereco { get; set; }
    public ICollection<Endereco> Enderecos { get; set; } = [];
    public ICollection<ClienteContato> Contatos { get; set; } = [];
    public ClienteDadosComerciais? DadosComerciais { get; set; }
    public ICollection<ClienteVeiculo> Veiculos { get; set; } = [];
    }
}
