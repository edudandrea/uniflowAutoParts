# Emissão, cancelamento e carta de correção de NF-e

## Configuração da empresa

Em **Configurações > Certificado e Tributações**, selecione o ambiente, salve os dados fiscais do emitente e configure a tributação. Endereço, inscrição estadual e código IBGE são necessários para emitir. Nome, CNPJ, inscrição estadual e endereço disponíveis no cadastro do HUB são sincronizados no login. Publique os dois backends e entre novamente para atualizar o perfil. Campos ausentes no HUB podem ser completados no formulário, incluindo o CNPJ, e são salvos na empresa logada. O código IBGE e a numeração devem ser configurados no AutoParts.

A série e o próximo número são mantidos no banco por empresa e ambiente. Antes da primeira emissão, confira a numeração já utilizada em outros emissores. O sistema impede retroceder a sequência conhecida localmente; ele não conhece automaticamente os números emitidos em outros sistemas. A migração é aplicada pelo backend ao iniciar.

O certificado A1 é carregado na aplicação e enviado somente para assinar e autenticar as solicitações. O backend não grava o PFX nem sua senha nas notas, eventos ou configurações fiscais. O armazenamento local do certificado já existente na aplicação continua sujeito à sessão do usuário; rascunhos fiscais não incluem o certificado.

## Autorizar uma venda

1. Selecione cliente, produtos, preços de venda e pagamentos que fechem o total.
2. Prepare a venda e revise origem, NCM e unidade dos itens, endereço fiscal, indicador de IE e consumidor final do destinatário.
3. Confirme **Emitir NF-e em produção** para transmitir uma nota real, ou use homologação para testes.
4. Aguarde a autorização e o protocolo. O XML autorizado pode ser baixado na revisão ou na lista de notas.

O backend valida o XML contra o pacote oficial de schemas, assina e reserva número, chave e XML no banco antes da transmissão. A autorização só é confirmada com protocolo correspondente à chave, ambiente e digest do XML enviado. O estoque registra a saída no registro da venda, antes da transmissão, inclusive com nota pendente; consultas e autorização não repetem a baixa.

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

A revisão da venda permite escolher o CSOSN apenas para a nota atual, sem alterar a configuração tributária da empresa. Para operação tributada sem permissão de crédito, pode-se selecionar 102, inclusive para CPF ou CNPJ não contribuinte. O código deve corresponder à operação e à orientação contábil. Sem escolha, mantém-se o código da empresa. A validação de 101 para destinatário contribuinte com direito a crédito permanece ativa.

### Diagnostico de falhas de comunicacao

O transporte SEFAZ registra inicio, servico, ambiente, destino, duracao, status HTTP, codigos cStat e excecoes de rede/timeout/XML. Nao registra XML fiscal, certificado A1, senha ou corpo da resposta. Ao investigar uma consulta com 502, compare esses registros do backend com os logs de erro do Nginx no frontend no mesmo horario. Um registro de inicio sem resposta deve ser correlacionado com reinicios ou encerramentos da instancia.

O frontend Docker define proxy_read_timeout e proxy_send_timeout de 90 segundos para permitir que a API devolva o resultado do cliente SEFAZ, cujo limite e 60 segundos. Esse ajuste nao corrige indisponibilidade da SEFAZ, erros TLS ou falhas do gateway externo; os logs devem identificar a causa. API_PROXY_TARGET deve apontar para a origem do servico backend (sem /api), nunca para o proprio frontend. Publicar backend e frontend e necessario para ativar estes ajustes na hospedagem.

A linha `SEFAZ: falha` inclui tambem `diagnostico`: tipo e mensagem da excecao e suas causas internas, em uma unica linha. Isso permite distinguir erros DNS/socket, negociacao TLS e resposta HTTP mesmo quando a interface de logs separa as linhas da excecao. O resumo limita a quantidade de causas e o tamanho de cada mensagem; o stack trace completo permanece no registro de warning.

### SecureConnectionError / PartialChain na SEFAZ/RS

O backend Docker instala a raiz publica ICP-Brasil v10 exigida pela cadeia SERPRO SSLv1 do servidor SEFAZ/RS, com verificacao de impressao digital no build. A validacao TLS permanece ativa. Veja [origem, fingerprint e manutencao](../back/certificates/README.md). Para corrigir uma instancia em execucao, reconstrua e publique o backend usando back/Dockerfile; reiniciar o container antigo nao aplica a mudanca. Depois consulte a mesma nota pendente antes de qualquer reenvio. Nao e necessario trocar o A1 da empresa nem alterar a Cloudflare por causa deste erro.

A listagem de notas exibe o ultimo codigo e motivo da autorizacao e o botao Reenviar XML. O dialogo apresenta o retorno completo da SEFAZ, situacao e protocolo. O reenvio utiliza somente o XML assinado salvo, com a mesma chave, nota e numeracao. Notas rejeitadas e nao localizadas (217) podem iniciar o fluxo quando possuem XML salvo e nao possuem recibo ou evento pendente. O backend consulta novamente a autorizadora e somente retransmite apos confirmar codigo 217; se encontrar autorizacao ou cancelamento, atualiza a nota sem novo envio. Rejeicoes que exigem alterar o XML devem ser corrigidas no fluxo da venda.

## Estoque e novas vendas com NF-e pendente

A venda é registrada junto com a nota, os itens e a numeração em uma transação local antes da transmissão à SEFAZ. Nesse registro é feita a baixa das quantidades vendidas, inclusive fracionadas, mesmo que a nota continue em processamento ou seja rejeitada. Adicionar produtos a um rascunho não movimenta estoque.

Após receber a identificação da nota registrada, use **Nova venda**. A tela limpa somente o rascunho local; a nota e a baixa permanecem registradas. Consulte e valide a nota no menu **NF-e**. Caso a resposta de transmissão se perca sem fornecer a identificação da nota, use **Recuperar tentativa** antes de descartá-la.

A autorização, consulta e retransmissão do mesmo XML não repetem a baixa. O cancelamento confirmado pela SEFAZ devolve a quantidade uma única vez. Um cancelamento sem confirmação mantém o estoque baixado até que a consulta confirme o evento.

Uma revisão de nota rejeitada informa `vendaOrigemId`, vinculando as tentativas à mesma venda. A revisão ajusta somente a diferença das quantidades (incluindo produtos removidos), sem baixar novamente toda a venda. Tentativas substituídas não podem ser retransmitidas e uma venda pendente, autorizada ou cancelada não pode ganhar outra revisão. A migração `LinkFiscalSaleStock` adiciona o vínculo; a API aplica as migrações na inicialização.

Vendas e NF-e utilizam SweetAlert2 nos avisos, resultados e confirmações. O retorno da SEFAZ também permanece na tela para consulta.
