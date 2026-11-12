# Emissão, cancelamento e carta de correção de NF-e

## Configuração da empresa

Em **Configurações > Certificado e Tributações**, selecione o ambiente, salve os dados fiscais do emitente e configure a tributação. Endereço, inscrição estadual e código IBGE são necessários para emitir. O CNPJ vem do cadastro da empresa autenticada.

A série e o próximo número são mantidos no banco por empresa e ambiente. Antes da primeira emissão, confira a numeração já utilizada em outros emissores. O sistema impede retroceder a sequência conhecida localmente; ele não conhece automaticamente os números emitidos em outros sistemas. A migração é aplicada pelo backend ao iniciar.

O certificado A1 é carregado na aplicação e enviado somente para assinar e autenticar as solicitações. O backend não grava o PFX nem sua senha nas notas, eventos ou configurações fiscais. O armazenamento local do certificado já existente na aplicação continua sujeito à sessão do usuário; rascunhos fiscais não incluem o certificado.

## Autorizar uma venda

1. Selecione cliente, produtos, preços de venda e pagamentos que fechem o total.
2. Prepare a venda e revise origem, NCM e unidade dos itens, endereço fiscal, indicador de IE e consumidor final do destinatário.
3. Confirme **Emitir NF-e em produção** para transmitir uma nota real, ou use homologação para testes.
4. Aguarde a autorização e o protocolo. O XML autorizado pode ser baixado na revisão ou na lista de notas.

O backend valida o XML contra o pacote oficial de schemas, assina e reserva número, chave e XML no banco antes da transmissão. A autorização só é confirmada com protocolo correspondente à chave, ambiente e digest do XML enviado. O estoque só registra a saída após autorização, uma vez por item.

Em timeout ou queda de conexão, a nota permanece em processamento. Reabra o rascunho ou consulte a nota na lista. A mesma solicitação recupera a nota existente sem transmitir uma nova. Após consulta com código 217, o sistema permite reenviar o mesmo XML, mantendo chave e número. Duplicidades 204/539 permanecem pendentes de conferência para evitar uma segunda emissão da mesma operação; confira a numeração e a nota preexistente na SEFAZ antes de regularizar.

Rejeições conclusivas exibem código e motivo e permitem revisar os dados para uma nova tentativa. O número reservado permanece no histórico; não há inutilização automática de lacunas nesta implementação.

## Cancelamento e CC-e

Na lista **Notas fiscais**, uma nota autorizada oferece cancelamento e carta de correção. Os serviços usam o ambiente registrado na própria nota, mesmo que a empresa tenha alterado seu ambiente padrão.

O cancelamento exige justificativa de 15 a 255 caracteres e confirmação de que não houve circulação da mercadoria nem realização da operação. O limite normal implementado para o RS é de 168 horas após autorização. A SEFAZ verifica as demais condições. Só depois da confirmação o estado muda para cancelada e o estoque recebe o estorno, uma única vez.

A CC-e exige texto de 15 a 1.000 caracteres e usa sequência de 1 a 20. Cada nova carta deve consolidar as correções anteriores que continuem válidas. Ela não pode alterar valores que determinam o imposto, remetente/destinatário ou datas de emissão/saída. Uma CC-e não modifica os valores ou o estoque da venda.

Eventos são persistidos antes do envio. Em resposta indeterminada, novos eventos ficam bloqueados até consulta da mesma nota. O histórico mostra situação, texto, código, protocolo e permite baixar o XML de cada evento confirmado.

## Cobertura e limites

Esta entrega atende **NF-e modelo 55, saída normal, operação interna no RS, Simples Nacional/MEI (CRT 1/4), CSOSN 101/102/103/300/400**, com PIS/COFINS por alíquota ou grupos não tributados. CSOSN 101 requer destinatário contribuinte com direito a crédito. A alíquota de ICMS configurada nesse caso representa o crédito do Simples.

NFC-e, contingência, operações interestaduais/exterior, substituição tributária, IPI, regimes com grupos IBS/CBS e outras finalidades fiscais exigem implementação adicional e são bloqueados neste fluxo. Não há geração automática de DANFE/PDF, inutilização de numeração ou cancelamento extemporâneo. Validar o schema não substitui a autorização da SEFAZ nem a revisão do enquadramento tributário.

Os testes automatizados usam certificados sintéticos e transporte SEFAZ simulado. Nenhuma nota real foi emitida durante a implementação. Para operar, publique o backend e o frontend atualizados, aplique as migrações e configure os dados reais da empresa, o certificado e a numeração.

## Referências oficiais

- [Serviços NF-e da SVRS e SEFAZ/RS](https://dfe-portal.svrs.rs.gov.br/Nfe/Servicos).
- [Documentos técnicos e schemas](https://dfe-portal.svrs.rs.gov.br/Nfe/Documentos).
- [Condições e prazo de cancelamento no RS](https://atendimento.receita.rs.gov.br/quais-sao-as-condicoes-e-prazos-para-o-cancelamento-de-uma-nf-e).
- [Manual de Orientação do Contribuinte, leiaute e regras](https://www.confaz.fazenda.gov.br/legislacao/arquivo-manuais/moc7-anexo-i-leiaute-e-rv.pdf).
