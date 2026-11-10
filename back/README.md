## Integracao com o HUB

O AutoParts agora exige sessao do HUB para acessar as APIs de negocio.
A empresa vem da identidade autenticada. A configuracao manual, as URLs de
entrada e callback, o vinculo dos dados existentes e os claims aceitos estao
em [HUB_SSO.md](../docs/HUB_SSO.md). Sem a configuracao, o acesso permanece fechado.

# API e PostgreSQL no Railway

Ao iniciar, a API executa `Database.MigrateAsync()` antes de aceitar requisições.
Se o banco indicado na conexão não existir, o Entity Framework tenta criá-lo.
Depois aplica somente as migrations ainda não registradas em
`__EFMigrationsHistory`. Reiniciar sem novas migrations não recria as tabelas.
Se a atualização falhar, a API registra o erro e interrompe a inicialização.

## Configuração inicial

É necessário um **serviço PostgreSQL separado** no Railway. O código cria um
banco dentro de um servidor PostgreSQL existente; não provisiona o serviço,
o volume ou as credenciais do Railway. O usuário da conexão precisa de permissão
para criar o banco (quando necessário) e alterar seu esquema.

No serviço da **API**, configure a variável abaixo. Neste exemplo, o serviço do
banco se chama `Postgres`; substitua pelo nome exato usado no projeto:

```text
ConnectionStrings__DefaultConnection=Host=${{Postgres.PGHOST}};Port=${{Postgres.PGPORT}};Database=${{Postgres.PGDATABASE}};Username=${{Postgres.PGUSER}};Password=${{Postgres.PGPASSWORD}}
Cors__AllowedOrigins__0=https://front-production-0fc8.up.railway.app
```

`DATABASE_URL` sozinha não é lida pela aplicação. Uma conexão ausente ou vazia
interrompe a inicialização com uma mensagem de configuração.

## Atualizações pelo GitHub

Mantenha o serviço da API conectado ao repositório e à branch desejada, com
deploys automáticos habilitados no Railway. O Dockerfile já inclui as migrations
na aplicação publicada; não é necessário instalar `dotnet ef` no container.
Cada deploy inicia a nova API e aplica as migrations pendentes automaticamente.

Quando alterar os modelos, crie uma migration no desenvolvimento e envie seus
arquivos junto com o código:

```powershell
cd back
dotnet ef migrations add NomeDaAlteracao --context AppDbContext
git add .
git commit -m "Atualiza esquema do banco"
git push
```

O deploy **aplica** migrations existentes; não gera migrations a partir dos
modelos. Revise alterações que removam dados antes de enviá-las. Mantenha backups
do banco: voltar uma versão da aplicação não desfaz migrations já aplicadas.
Não use `EnsureCreated` junto com esse fluxo de migrations.

Nos logs de inicialização, procure `Preparando o banco PostgreSQL` e
`Banco PostgreSQL atualizado`. Se houver falha, consulte a exceção registrada
imediatamente depois de `Falha ao preparar o banco PostgreSQL`.

## Consulta de estoque

`GET /api/estoque?empresaId=1` retorna os produtos de `CadastroItens` da empresa,
com marca, categoria, preço e mínimo cadastrados. O saldo é calculado a partir
dos movimentos de `Estoques` da mesma empresa e produto. Movimentos do tipo
`Saida` ou `Saída` descontam o valor absoluto da quantidade; os demais somam
a quantidade com seu sinal. Produtos sem movimentos têm saldo zero.

A importação da NF-e cadastra produtos ausentes e lança uma entrada em `Estoques`
para cada item associado, usando a quantidade recebida (inicialmente a quantidade
comercial do XML). Quantidades fracionadas são preservadas com quatro casas decimais.
Reimportar uma chave existente reabre a nota e completa movimentos ausentes, sem
duplicar o saldo. Salvar a conferência ajusta o movimento do item; concluir bloqueia
novas alterações. Itens sem produto associado ficam pendentes de entrada no estoque.
Notas importadas antes desta atualização podem ser reimportadas para completar seus
movimentos. A migration `AddNfeStockMovements` adiciona o vínculo único por item e
amplia `Estoques.Quantidade` de inteiro para decimal, preservando as quantidades atuais.
Localização, peso, imagem e código de barras não estão no cadastro atual do
produto e não recebem valores fictícios na tela.

## Notas fiscais emitidas (NF-e / NFC-e)

A pagina `/app/notas` consulta `GET /api/notas-fiscais?empresaId=1` com paginacao
fixa de 10 registros no servidor. Busca por numero, chave, cliente ou CPF/CNPJ,
tipo, situacao, periodo de emissao, serie, ambiente e intervalo de valores.
Os indicadores refletem todos os registros dos filtros, nao apenas a pagina.
As datas de pesquisa representam dias em UTC-03 (Sao Paulo).

A migration `AddNotasFiscaisEmitidas` adiciona `NotasFiscais` e
`NotaFiscalEventos`, preservando as entradas de compra existentes. A API aplica
essa migration no startup, como as demais. A listagem comeca vazia; nao ha seeds.
O frontend segue a empresa 1 usada nas demais telas do projeto.

`POST /api/notas-fiscais/importar` aceita multipart com `empresaId`, `xml`
obrigatorio e `pdf` opcional (ate 5 MB cada). Aceita XML processado `nfeProc`,
modelo 55 ou 65 de saida com protocolo de autorizacao e CNPJ do emitente igual
ao da empresa cadastrada. O XML original e o PDF fornecido sao preservados no
banco. A importacao reflete o protocolo do arquivo; nao constitui validacao
online da autenticidade nem consulta eventos posteriores. Nao emite uma nota.
O PDF precisa ser o DANFE correspondente fornecido pelo usuario; nao existe
um gerador de DANFE nesta implementacao.

Downloads: `GET /api/notas-fiscais/{id}/xml?empresaId=1` e `/pdf?empresaId=1`.
Sem PDF anexado, a acao fica desabilitada. O CSV exporta somente a pagina atual.

### Eventos diretos na SEFAZ/RS em homologacao

A integracao conserva o escopo do certificado atual: **RS, homologacao,
emissao normal**. Nao troca automaticamente uma nota de producao para teste.
Documentos de outras UFs, producao ou contingencia permitem consulta local e
download, mas seus eventos nao sao transmitidos por esta implementacao.

- `POST /api/notas-fiscais/{id}/cancelar?empresaId=1`: `{ texto, base64, password }`,
  justificativa de 15 a 255 caracteres, evento 110111, sequencia 1.
- `POST /api/notas-fiscais/{id}/carta-correcao?empresaId=1`: mesmo corpo, texto de
  15 a 1000 caracteres, evento 110110, sequencia de 1 a 20. Somente modelo 55.
- `POST /api/notas-fiscais/{id}/consultar?empresaId=1`: `{ base64, password }`.
  Atualiza a situacao e reconcilia eventos presentes na consulta de protocolo.
  Use antes de enviar eventos se a nota foi movimentada em outro sistema.

O navegador envia o A1 previamente cadastrado apenas na solicitacao. A senha e
a chave privada nao sao gravadas nas tabelas fiscais. O evento e assinado com
XMLDSig e transmitido via SOAP 1.2/mTLS ao endpoint oficial da UF/modelo.
XML enviado, retorno, motivo, sequencia e protocolo sao registrados no banco.
A nota so muda para Cancelada com evento 135 com protocolo confirmado, ou
quando a consulta oficial confirma seu cancelamento. Processamento de lote
128, duplicidade 573 e evento nao vinculado 136 nao significam sucesso local.

Um advisory lock PostgreSQL impede envio simultaneo da mesma nota em replicas.
O evento pendente e persistido antes da chamada externa. Timeout, interrupcao
ou resposta incoerente mantem o resultado indeterminado e bloqueiam novos
eventos. A acao de consulta na tabela reconcilia eventos confirmados retornados
pela SEFAZ. Se a consulta nao trouxer o evento, o bloqueio permanece e exige
conferencia fiscal; nao se presume que a solicitacao falhou nem se reenvia.

Referencias oficiais:
- [Servicos NF-e e URLs da SEFAZ/RS](https://dfe-portal.svrs.rs.gov.br/Nfe/Servicos)
- [Servicos NFC-e](https://dfe-portal.svrs.rs.gov.br/Nfce/Servicos)
- [Manual de Orientacao do Contribuinte](https://www.nfe.fazenda.gov.br/portal/exibirArquivo.aspx?conteudo=LrBx7WT9PuA%3D)

Validacao local, sem transmitir documentos:

```powershell
dotnet run --project tests/FiscalChecks/FiscalChecks.csproj
cd front
npm test -- --watch=false --include=src/app/pages/invoices/invoices.component.spec.ts
npm run build
```

Os testes verificam parsing e isolamento do emitente, assinatura XML, limites,
correlacao dos retornos, filtros, pagina de 10, downloads e fluxos de evento.
A aceitacao real na SEFAZ requer certificado, credenciamento e documento
validos de homologacao; nao foi executada com credenciais fiscais reais.
