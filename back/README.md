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
dotnet ef migrations add NomeDaAlteracao
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
