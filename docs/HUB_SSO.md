# AutoParts integrado ao HUB UniFlow

O AutoParts recebe a identidade da central do UniflowPeople por OpenID Connect
(Authorization Code com PKCE S256). A autenticacao e a concessao de acesso ao
projeto continuam no HUB. O AutoParts nao recebe nem armazena a senha do HUB.
Nenhuma alteracao no UniflowPeople e necessaria para os claims basicos atuais;
a configuracao do projeto na central sera feita manualmente.

## Configuracao manual no HUB

Na central, abra o cadastro do projeto AutoParts, configure e habilite o SSO:

| Campo | Valor |
| --- | --- |
| URL do projeto | `https://parts.uniflowtech.com.br/` |
| URL de entrada SSO | `https://parts.uniflowtech.com.br/sso` |
| URI de retorno permitida | `https://parts.uniflowtech.com.br/api/auth/callback` |
| Client ID | `uniflow-project-ID_DO_PROJETO` |
| Fluxo | Authorization Code, PKCE S256, cliente publico sem segredo |
| Escopos | `openid profile email` |

Substitua ID_DO_PROJETO pelo ID numerico real da central (maior que 1).
O HUB exige conta ativa, permissao para o projeto e assinatura valida para o
cliente. Nao use o ID do projeto People. Cada ambiente precisa registrar sua
URI de retorno exata; nao utilize curingas.

## Configuracao do AutoParts no Railway

No servico **backend**, preserve a conexao PostgreSQL e configure:

```text
HubAuth__Enabled=true
HubAuth__Authority=https://auth.uniflowtech.com.br/
HubAuth__PublicOrigin=https://parts.uniflowtech.com.br/
HubAuth__ProjectId=ID_NUMERICO_REAL_DO_PROJETO
HubAuth__DataProtectionPath=/data/autoparts-keys
```

Monte um volume persistente em `/data`, com permissao de leitura e escrita
somente para o servico. As chaves de sessao sao proprias do AutoParts: nao copie
certificados nem chaves JWT do HUB. Replicas precisam usar as mesmas chaves de
Data Protection. Preserve estas chaves entre deploys.

No servico **frontend**, configure:

```text
API_PROXY_TARGET=https://DOMINIO_REAL_DO_BACKEND
```

Use a origem do backend, sem `/api` no final. No Docker de producao, o Nginx encaminha `/api/**` ao backend informado em `API_PROXY_TARGET`, preservando os caminhos de login, sessao e callback. O container exige uma origem HTTP(S) sem caminho, query ou credenciais. O comando `npm run start:railway`
usa o proxy configurado em `angular.json`. O navegador acessa `/api` no proprio
dominio `parts.uniflowtech.com.br`, inclusive no callback OIDC. Isso mantem o
cookie HttpOnly no dominio do sistema e evita depender de cookies de terceiros
no dominio publico do Railway. Se usar outra hospedagem estatica no futuro,
configure nela o mesmo encaminhamento de `/api/**` para o backend.

Publique backend e frontend juntos com essas configuracoes. A API aplica a
migration `AddHubIdentityAndTenantIsolation` na inicializacao. A integracao
vem desativada no arquivo versionado; mesmo desativada, APIs de negocio exigem
sessao e nao liberam a empresa 1 como alternativa.

## Dados da identidade recebida

| Claim | Obrigatorio | Uso |
| --- | --- | --- |
| `sub` | Sim | Identificador estavel do usuario, por exemplo `central-7` |
| `tenant_id` | Sim | Chave do cliente, por exemplo `central-3` ou `people-2` |
| `project_id` | Sim | Deve coincidir com o projeto configurado neste backend |
| `name`, `email` | Perfil | Nome e email exibidos do usuario autenticado |
| `role` | Perfil | Perfil concedido pelo HUB para este projeto |
| `tenant_name` | Opcional | Nome da empresa, se o HUB disponibilizar esse claim |
| `tenant_cnpj` | Opcional | CNPJ da empresa, se o HUB disponibilizar esse claim |

Assinatura, emissor, destinatario, validade, state, nonce e PKCE sao validados
pelo middleware OIDC nativo. Um token de outro projeto nao concede acesso.
Nao se aceita token JWT, empresa ou usuario pela query string da pagina.
O cookie local tem duracao maxima de 20 minutos sem renovacao deslizante.
Um novo login pode aproveitar a sessao existente do HUB. A cada requisicao, o
AutoParts tambem verifica se empresa e usuario continuam ativos localmente.

O HUB atual envia os claims basicos acima, mas nao envia nome e CNPJ do cliente
no token. **Sem esses dados, o AutoParts nao inventa um nome nem um CNPJ**: cria
o vinculo isolado e informa que os dados cadastrais estao pendentes. Para exibir
esses dados sem alterar o HUB, configure o perfil no proprio AutoParts:

```text
HubAuth__Companies__0__TenantId=CHAVE_REAL_DO_CLIENTE
HubAuth__Companies__0__Name=NOME_REAL_DA_EMPRESA
HubAuth__Companies__0__Cnpj=CNPJ_REAL_COM_14_DIGITOS
```

Os dados enviados em claims autenticados tem precedencia sobre esse perfil.
Use indices 1, 2, etc. para outros clientes quando necessario. O usuario nao
pode alterar esses dados por parametros de URL ou cabecalhos da requisicao.

## Associar os dados ja existentes

Os registros atuais nao sao movidos, redistribuidos nem apagados. Os IDs locais
de empresa nao precisam ser iguais aos IDs do HUB. O vinculo usa o par
`emissor + tenant_id`, e cada cliente novo recebe uma empresa local propria.

Antes do primeiro acesso, associe explicitamente a chave do cliente correto a
empresa local que ja possui os cadastros. Exemplo de sintaxe (substitua ambos
pelos valores reais; a empresa local 1 e apenas um exemplo):

```text
HubAuth__Companies__0__TenantId=CHAVE_REAL_DO_DONO_DOS_DADOS
HubAuth__Companies__0__LocalEmpresaId=1
HubAuth__Companies__0__Name=NOME_REAL_DA_EMPRESA
HubAuth__Companies__0__Cnpj=CNPJ_REAL_COM_14_DIGITOS
```

Nao atribua a mesma empresa local a clientes diferentes. Nao existe associacao
automatica por email ou CNPJ: essa associacao poderia entregar registros
existentes ao cliente errado. Um vinculo ja estabelecido nao e transferido para
outra chave automaticamente. Sem LocalEmpresaId, o primeiro acesso cria uma
empresa nova e vazia, preservando os registros antigos sem um dono presumido.

## Isolamento e operacao

Todas as rotas de negocio exigem sessao. A empresa e obtida do cookie validado
pelo backend. Se um parametro, JSON ou formulario tentar indicar outra empresa,
a API rejeita a solicitacao. Consultas EF possuem filtros globais, inclusive
nas tabelas filhas; gravacoes validam propriedade, alteracoes e relacionamentos
para impedir referencias entre empresas. Downloads e uploads mantem o prefixo
de armazenamento da empresa autenticada.

Os IDs fixos de empresa foram removidos das telas que usam a API. Certificados,
tributacao e rascunhos locais usam chaves por empresa e usuario. Chaves antigas
de empresa 1 nao sao carregadas nem atribuidas a um cliente automaticamente.
Troca de identidade em outra aba invalida a tela antiga; a API continua sendo
a autoridade sobre o acesso, independentemente desse mecanismo do navegador.

O cabecalho exibe a empresa e o usuario, com **Minhas aplicacoes** e **Sair do
sistema**. Sair encerra a sessao local e retorna ao seletor do HUB. Logout
federado nao existe no contrato atual do HUB: sair ou revogar uma assinatura
na central impede novos logins, mas uma sessao local existente pode durar ate
20 minutos. Bloqueios locais da empresa ou usuario sao verificados em cada
requisicao.

Vendas ainda possuem o fluxo de rascunho existente; esta integracao isola esses
rascunhos e suas consultas de clientes/produtos, mas nao acrescenta persistencia
de vendas nem implementa funcionalidades de negocio que ainda nao existiam.

## Desenvolvimento e verificacao

Em desenvolvimento, informe Authority, PublicOrigin e ProjectId do ambiente
de homologacao e registre a URI de retorno correspondente no HUB. HTTP somente
e aceito em localhost quando o backend esta em Development. O proxy local usa
`http://localhost:5031`, ou o valor informado em API_PROXY_TARGET.

```powershell
dotnet run --project tests/TenantChecks/TenantChecks.csproj
dotnet run --project tests/FiscalChecks/FiscalChecks.csproj
dotnet ef migrations has-pending-model-changes --context AppDbContext --project back/UniflowAutoParts.Api.csproj
cd front
npm test -- --watch=false
npm run build
```

Os testes HTTP usam banco descartavel e identidades exclusivas do executavel de
testes; nao adicionam acesso de teste ao sistema real. Verificam duas empresas,
URLs adulteradas, escrita em outra empresa, tabelas filhas, saldos, CSRF e
gravacoes diretas por Attach. Os testes de frontend verificam sessao, escopo do
armazenamento e protecao das requisicoes. O login real exige configuracao manual
do projeto no HUB e publicacao; nao foi executado com credenciais de clientes.

Referencias: [cliente OIDC ASP.NET Core](https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication),
[filtros por tenant no EF Core](https://learn.microsoft.com/ef/core/querying/filters),
[proxy do Angular CLI](https://angular.dev/tools/cli/serve).

## Diagnostico de entrada e proxy

Na central, use `https://parts.uniflowtech.com.br/sso` como entrada SSO.
A rota `/entrar` e a tela de entrada manual; o caminho `/sso` inicia OIDC.

Acesse `https://parts.uniflowtech.com.br/api/auth/session` sem login:
o retorno deve ser JSON com `authenticated: false` e `available: true`.
HTML com titulo UniFlow AutoParts indica que `/api` caiu no frontend:
confira o proxy Nginx e a variavel API_PROXY_TARGET no servico frontend.
`available: false` indica que HubAuth__Enabled/configuracao da API esta pendente.

`/api/auth/login` deve iniciar o redirecionamento ao emissor OIDC, sem
retornar index.html. O callback `/api/auth/callback` tambem deve chegar ao
backend; ele so conclui com o state, nonce e codigo de um login valido.