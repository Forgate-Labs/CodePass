# CodePass

CodePass é uma plataforma self-hosted de qualidade de código para soluções C#/.NET. O foco do projeto é oferecer análise local, leve e profundamente integrada ao ecossistema .NET, com regras customizáveis em JSON executadas sobre Roslyn.

A proposta é parecida com ferramentas de qualidade como SonarQube, mas com escopo mais focado: C#/.NET, execução local, regras autorais e uma experiência simples para registrar uma `.sln`, rodar análises e entender se o projeto passa ou falha.

## Funcionalidades principais

- Cadastro de soluções locais por caminho de arquivo `.sln`.
- Validação e atualização do status das soluções cadastradas.
- Criação e edição de regras customizadas pelo editor schema-driven.
- Edição de regras em raw JSON.
- Seleção de regras por solução cadastrada.
- Análise de regras com Roslyn.
- Persistência de execuções e violações encontradas.
- Análise de cobertura via `dotnet test --collect:"XPlat Code Coverage"`.
- Normalização de cobertura em nível de projeto e classe.
- Dashboard com quality score, status pass/fail e detalhamento das contribuições de regras e cobertura.
- API local para agentes executarem análise de qualidade.
- CLI para análise por linha de comando.

## Stack atual

- .NET 10
- ASP.NET Core / Blazor Server
- Entity Framework Core
- SQLite
- Roslyn / MSBuildWorkspace
- Cobertura como formato normalizado de cobertura
- xUnit, bUnit e FluentAssertions nos testes

## Estrutura do repositório

```txt
src/
  CodePass.Web/      Aplicação web Blazor, serviços, persistência e analyzers.
  CodePass.Cli/      CLI para executar análise de regras/cobertura.

tests/
  CodePass.Web.Tests/ Testes automatizados da Web, serviços e analyzers.

.planning/           Documentação de planejamento, requisitos, roadmap e estado.
.planning/research/  Pesquisa arquitetural e de produto.
```

## Instalação e uso

Pré-requisitos:

- .NET SDK 10 instalado.
- Acesso local aos repositórios/solutions que serão analisados.
- Para cobertura, os projetos de teste da solution analisada precisam conseguir rodar com `dotnet test --collect:"XPlat Code Coverage"`.

Restaure e valide o repositório:

```bash
dotnet restore CodePass.sln
dotnet test CodePass.sln
```

### Web

A Web é a forma principal de uso do CodePass. Ela permite cadastrar solutions, criar regras, selecionar regras por solution, executar análises e visualizar dashboard.

#### Executar em modo desenvolvimento

```bash
dotnet run --project src/CodePass.Web --urls http://localhost:5000
```

Depois acesse:

```txt
http://localhost:5000
```

Por padrão, a Web usa SQLite com connection string:

```json
"Data Source=codepass.db"
```

A aplicação inicializa o banco local automaticamente na subida. O arquivo `codepass.db` será criado no diretório de trabalho do processo.

#### Publicar e executar a Web

```bash
dotnet publish src/CodePass.Web -c Release -o ./publish/codepass-web
```

Execute a aplicação publicada:

```bash
dotnet ./publish/codepass-web/CodePass.Web.dll --urls http://localhost:5000
```

#### Fluxo básico na Web

1. Abra a aplicação no navegador.
2. Vá em **Solutions** e cadastre o caminho absoluto de uma `.sln` local.
3. Vá em **Rules** e crie uma regra pelo formulário ou pelo modo raw JSON.
4. Vá em **Rule Analysis** e habilite as regras desejadas para a solution.
5. Execute a análise de regras.
6. Vá em **Coverage Analysis** e execute a análise de cobertura.
7. Abra o **Dashboard** para ver score, status pass/fail e evidências.

### CLI

A CLI permite executar análise sem abrir a UI. Ela é útil para automação local, scripts e validações rápidas.

#### Executar a CLI pelo projeto

```bash
dotnet run --project src/CodePass.Cli -- analyze \
  --solution CodePass.sln \
  --rules ./rules.json \
  --coverage \
  --output codepass-quality.json
```

#### Instalar a CLI como ferramenta global

Gere o pacote NuGet local:

```bash
dotnet pack src/CodePass.Cli -c Release -o ./artifacts
```

Instale como `dotnet tool` global:

```bash
dotnet tool install --global CodePass.Tool --add-source ./artifacts
```

Depois use o comando `codepass` de qualquer diretório:

```bash
codepass analyze \
  --solution /caminho/para/App.sln \
  --rules /caminho/para/rules.json \
  --coverage \
  --output /caminho/para/codepass-quality.json
```

Para atualizar uma instalação global local:

```bash
dotnet pack src/CodePass.Cli -c Release -o ./artifacts
dotnet tool update --global CodePass.Tool --add-source ./artifacts
```

Para remover:

```bash
dotnet tool uninstall --global CodePass.Tool
```

#### Opções principais da CLI

- `--solution <path>`: caminho da `.sln`.
- `--rules <path>`: arquivo JSON ou pasta com regras JSON.
- `--coverage`: executa cobertura.
- `--output <path>`: salva resultado JSON.
- `--min-line-coverage <n>`: mínimo de cobertura de linhas.
- `--min-branch-coverage <n>`: mínimo de cobertura de branches.
- `--pass-threshold <n>`: score mínimo. Padrão: `80`.
- `--fail-on-rule-errors <bool>`: falha se houver violações `error`. Padrão: `true`.
- `--fail-on-rule-warnings <bool>`: falha se houver violações `warning`. Padrão: `false`.
- `--quiet`: reduz logs de progresso.

#### Exemplos de uso da CLI

Somente regras:

```bash
codepass analyze \
  --solution /caminho/para/App.sln \
  --rules /caminho/para/rules.json
```

Somente cobertura:

```bash
codepass analyze \
  --solution /caminho/para/App.sln \
  --coverage
```

Regras e cobertura com quality gate:

```bash
codepass analyze \
  --solution /caminho/para/App.sln \
  --rules /caminho/para/rules \
  --coverage \
  --min-line-coverage 80 \
  --min-branch-coverage 70 \
  --pass-threshold 85 \
  --output codepass-quality.json
```

A CLI retorna código de saída `0` quando o quality gate passa e `1` quando falha.

## Regras customizadas

As regras do CodePass são sempre C# e usam JSON estruturado. O campo `language` não deve ser informado no JSON de autoria.

Formato base:

```json
{
  "id": "CP1000",
  "title": "Título da regra",
  "description": "Descrição curta.",
  "kind": "method_metrics",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "maxLines": 50
  }
}
```

Kinds suportados atualmente incluem:

- `syntax_presence`
- `forbidden_api_usage`
- `symbol_naming`
- `attribute_policy`
- `dependency_policy`
- `method_metrics`
- `class_metrics`
- `interface_metrics`
- `inheritance_contract_policy`
- `polymorphism_opportunity`
- `architecture_policy`
- `dependency_inversion_policy`
- `exception_handling`
- `async_policy`

Veja detalhes completos em [`rules-json-guide.md`](rules-json-guide.md).

## API local para agentes

Quando o CodePass Web está rodando, agentes locais podem usar endpoints HTTP para listar soluções cadastradas e executar análise de qualidade.

Fluxo:

```http
GET /api/agent-quality/solutions
POST /api/agent-quality/solutions/{registeredSolutionId}/analyze
```

A documentação completa fica em [`SKILL.md`](SKILL.md).

## Arquivos Markdown do projeto

### Documentação principal

- [`README.md`](README.md): visão geral do projeto, execução, testes, CLI e mapa da documentação.
- [`rules-json-guide.md`](rules-json-guide.md): guia atual e prático do formato JSON de regras aceito pela Web e pela CLI.
- [`dotnet-lint-dsl-guide.md`](dotnet-lint-dsl-guide.md): estudo conceitual sobre como modelar uma DSL estruturada para regras de lint .NET usando Roslyn.
- [`SKILL.md`](SKILL.md): instruções para agentes locais usarem os endpoints de qualidade do CodePass.
- [`MEMORY.md`](MEMORY.md): memória operacional com erros anteriores e regras preventivas para futuras alterações.

### Planejamento

- [`.planning/PROJECT.md`](.planning/PROJECT.md): definição do produto, valor central, contexto, restrições e decisões principais.
- [`.planning/REQUIREMENTS.md`](.planning/REQUIREMENTS.md): requisitos v1/v2, itens fora de escopo e rastreabilidade.
- [`.planning/ROADMAP.md`](.planning/ROADMAP.md): fases do roadmap e progresso por fase/plano.
- [`.planning/STATE.md`](.planning/STATE.md): estado atual do projeto, progresso, decisões acumuladas e continuidade de sessão.

### Pesquisa

- [`.planning/research/SUMMARY.md`](.planning/research/SUMMARY.md): resumo executivo da pesquisa de produto, arquitetura, stack e riscos.
- [`.planning/research/ARCHITECTURE.md`](.planning/research/ARCHITECTURE.md): pesquisa arquitetural e desenho recomendado.
- [`.planning/research/FEATURES.md`](.planning/research/FEATURES.md): análise de funcionalidades esperadas, diferenciais e anti-features.
- [`.planning/research/STACK.md`](.planning/research/STACK.md): pesquisa de stack recomendada e justificativas.
- [`.planning/research/PITFALLS.md`](.planning/research/PITFALLS.md): riscos críticos, sinais de alerta e formas de mitigação.

## Escopo e decisões importantes

- O v1 é focado apenas em C#/.NET.
- As regras ativas são autorais; o produto não depende de rule packs de produção embutidos.
- O catálogo de `kind`s é fechado e controlado pelo engine.
- O JSON de regras é validado contra os campos suportados por cada `kind`.
- A análise de regras é baseada em Roslyn, não em busca textual simples.
- O dashboard mostra o estado atual; histórico e tendências ficam para fases futuras.

## Status

O planejamento v1 está marcado como completo em `.planning/STATE.md`, com as fases principais implementadas:

1. Soluções cadastradas.
2. Regras autorais.
3. Análise de regras.
4. Análise de cobertura.
5. Dashboard de quality score.
