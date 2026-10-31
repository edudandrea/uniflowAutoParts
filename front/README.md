# UniflowAutoPartsApp

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.19.

## API e Railway

O build de produção (`npm run build`) e `npm run start:railway` usam
`https://back-production-b172.up.railway.app/api`, configurado em
`src/environments/environment.ts`. O desenvolvimento (`npm start`) usa
`http://localhost:5031/api` por meio de `environment.development.ts`.

Depois de publicar as alterações, faça um novo deploy do frontend no Railway.
Se a URL pública do backend mudar, atualize `environment.ts` e gere outro build.

No serviço **backend** do Railway, configure a variável
`Cors__AllowedOrigins__0=https://front-production-0fc8.up.railway.app`
(sem barra no final). Se o domínio do frontend for diferente, use a origem
correta. A API lê essa configuração em `Program.cs` para permitir as chamadas
do navegador. Para desenvolvimento local, pode adicionar
`Cors__AllowedOrigins__1=http://localhost:4200` no ambiente da API local.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
