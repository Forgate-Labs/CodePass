# Guia de regras JSON do CodePass

Este guia descreve o formato atual de regras JSON que podem ser criadas no CodePass e executadas pela análise de regras.

As regras são **sempre C#**. Não inclua `"language": "csharp"` no JSON de autoria da Web.

## Formato base

Toda regra em raw JSON deve ser um objeto com esta estrutura:

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

## Campos obrigatórios

| Campo | Tipo | Descrição |
|---|---:|---|
| `id` | string | Código único da regra, por exemplo `CP1000`. |
| `title` | string | Nome exibido na UI e nos resultados. |
| `kind` | string | Tipo de regra suportado pelo engine. |
| `schemaVersion` | string | Versão do schema. Hoje use `1.0`. |
| `severity` | string | Severidade: `info`, `warning` ou `error`. |
| `enabled` | boolean | Se a regra fica habilitada globalmente. |
| `scope` | object | Escopo de projetos/arquivos. |
| `parameters` | object | Parâmetros específicos do `kind`. |

`description` é opcional e pode ser `null` ou string.

## Scope

Todos os kinds usam o mesmo formato de `scope`:

```json
{
  "projects": ["*"],
  "files": ["**/*.cs"],
  "excludeFiles": []
}
```

| Campo | Tipo | Obrigatório | Descrição |
|---|---:|---:|---|
| `projects` | array de string | não | Padrões de nomes de projeto incluídos. Default prático: `["*"]`. |
| `files` | array de string | não | Padrões de arquivos incluídos. Default prático: `["**/*.cs"]`. |
| `excludeFiles` | array de string | não | Padrões de arquivos excluídos. |

Exemplo excluindo migrations e generated code:

```json
{
  "projects": ["*"],
  "files": ["**/*.cs"],
  "excludeFiles": ["**/Migrations/*.cs", "**/*.g.cs"]
}
```

## Kinds suportados atualmente

- `syntax_presence`
- `forbidden_api_usage`
- `symbol_naming`
- `attribute_policy`
- `dependency_policy`
- `method_metrics`
- `exception_handling`
- `async_policy`

A Web valida `parameters` por kind. Campos fora do schema são rejeitados.

---

## 1. `syntax_presence`

Requer ou proíbe construções de sintaxe C# suportadas.

### Parameters

| Campo | Tipo | Obrigatório | Valores |
|---|---:|---:|---|
| `mode` | string | sim | `forbid`, `require` |
| `targets` | array de string | sim | ver lista abaixo |
| `syntaxKinds` | array de string | sim | `var`, `goto`, `expression_bodied_member`, `missing_braces` |
| `allowInTests` | boolean | não | `true` ou `false` |

Valores aceitos em `targets`:

- `local_declaration`
- `foreach_variable`
- `for_initializer`
- `using_declaration`
- `lambda_parameter`
- `field_declaration`
- `property_declaration`
- `method_declaration`
- `class_declaration`
- `record_declaration`
- `member_access`

> Observação: na execução atual, o analyzer usa principalmente `syntaxKinds` dentro do `scope`. `targets` é parte do schema validado, mas ainda não refina todos os casos de execução.

### Exemplo: proibir `var`

```json
{
  "id": "CP1001",
  "title": "Evitar var",
  "description": "Use tipo explícito.",
  "kind": "syntax_presence",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "mode": "forbid",
    "targets": ["local_declaration"],
    "syntaxKinds": ["var"],
    "allowInTests": false
  }
}
```

### Exemplo: exigir braces

```json
{
  "id": "CP1002",
  "title": "Exigir braces",
  "description": "Evita if sem bloco.",
  "kind": "syntax_presence",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "mode": "forbid",
    "targets": ["method_declaration"],
    "syntaxKinds": ["missing_braces"]
  }
}
```

---

## 2. `forbidden_api_usage`

Proíbe uso de APIs/símbolos específicos usando análise semântica Roslyn.

### Parameters

| Campo | Tipo | Obrigatório | Descrição |
|---|---:|---:|---|
| `forbiddenSymbols` | array de string | sim | Símbolos proibidos. |
| `allowedAlternatives` | array de string | não | Alternativas exibidas na mensagem. |
| `allowInTests` | boolean | não | `true` ou `false`. |

### Exemplo: proibir `Console.WriteLine`

```json
{
  "id": "CP2001",
  "title": "Evitar Console.WriteLine",
  "description": "Use logger.",
  "kind": "forbidden_api_usage",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "forbiddenSymbols": ["System.Console.WriteLine"],
    "allowedAlternatives": ["ILogger.LogInformation"],
    "allowInTests": false
  }
}
```

---

## 3. `symbol_naming`

Valida convenções de nomes para símbolos declarados.

### Parameters

| Campo | Tipo | Obrigatório | Valores |
|---|---:|---:|---|
| `symbolKinds` | array de string | sim | `class`, `interface`, `method`, `property`, `field` |
| `capitalization` | string | sim | `camelCase`, `PascalCase` |
| `requiredPrefix` | string | não | Prefixo obrigatório. |
| `allowRegex` | string | não | Regex que aprova exceções. |

### Exemplo: fields privados com `_camelCase`

```json
{
  "id": "CP3001",
  "title": "Fields com underscore",
  "description": "Padroniza fields.",
  "kind": "symbol_naming",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "symbolKinds": ["field"],
    "capitalization": "camelCase",
    "requiredPrefix": "_",
    "allowRegex": ""
  }
}
```

### Exemplo: classes em PascalCase

```json
{
  "id": "CP3002",
  "title": "Classes em PascalCase",
  "description": "Padroniza classes.",
  "kind": "symbol_naming",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "symbolKinds": ["class"],
    "capitalization": "PascalCase",
    "requiredPrefix": "",
    "allowRegex": ""
  }
}
```

---

## 4. `attribute_policy`

Requer ou proíbe atributos em declarações C#.

### Parameters

| Campo | Tipo | Obrigatório | Valores |
|---|---:|---:|---|
| `mode` | string | sim | `require`, `forbid` |
| `targetKinds` | array de string | sim | `class`, `interface`, `method`, `property`, `field` |
| `attributes` | array de string | sim | Nomes dos atributos. |
| `matchInherited` | boolean | não | Considerar atributos herdados. |

### Exemplo: exigir `Authorize` em classes

```json
{
  "id": "CP4001",
  "title": "Exigir Authorize",
  "description": "Classes devem ser protegidas.",
  "kind": "attribute_policy",
  "schemaVersion": "1.0",
  "severity": "error",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*Controller.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "mode": "require",
    "targetKinds": ["class"],
    "attributes": ["Authorize"],
    "matchInherited": true
  }
}
```

### Exemplo: proibir `Obsolete`

```json
{
  "id": "CP4002",
  "title": "Proibir Obsolete",
  "description": "Evita APIs obsoletas.",
  "kind": "attribute_policy",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "mode": "forbid",
    "targetKinds": ["class", "method", "property"],
    "attributes": ["Obsolete"],
    "matchInherited": false
  }
}
```

---

## 5. `dependency_policy`

Proíbe dependências em namespaces ou tipos.

### Parameters

| Campo | Tipo | Obrigatório | Descrição |
|---|---:|---:|---|
| `sourceNamespaces` | array de string | não | Namespaces onde a regra se aplica. Vazio aplica geral. |
| `forbiddenNamespaces` | array de string | não | Namespaces proibidos. |
| `forbiddenTypes` | array de string | não | Tipos proibidos. |

### Exemplo: domínio não depende de infraestrutura

```json
{
  "id": "CP5001",
  "title": "Domínio sem infraestrutura",
  "description": "Mantém camadas separadas.",
  "kind": "dependency_policy",
  "schemaVersion": "1.0",
  "severity": "error",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "sourceNamespaces": ["MyApp.Domain"],
    "forbiddenNamespaces": ["MyApp.Infrastructure"],
    "forbiddenTypes": []
  }
}
```

### Exemplo: proibir tipo específico

```json
{
  "id": "CP5002",
  "title": "Proibir DateTime.Now",
  "description": "Use relógio injetado.",
  "kind": "dependency_policy",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "sourceNamespaces": [],
    "forbiddenNamespaces": [],
    "forbiddenTypes": ["System.DateTime"]
  }
}
```

---

## 6. `method_metrics`

Limita métricas simples de métodos.

### Parameters

| Campo | Tipo | Obrigatório | Descrição |
|---|---:|---:|---|
| `maxLines` | number inteiro | não | Máximo de linhas por método. |
| `maxParameters` | number inteiro | não | Máximo de parâmetros por método. |
| `maxCyclomaticComplexity` | number inteiro | não | Máximo de complexidade ciclomática. |

Valores `<= 0` desativam o limite correspondente na execução atual.

### Exemplo

```json
{
  "id": "CP6001",
  "title": "Limitar métodos",
  "description": "Métodos menores são melhores.",
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
    "maxLines": 50,
    "maxParameters": 5,
    "maxCyclomaticComplexity": 10
  }
}
```

---

## 7. `exception_handling`

Detecta padrões arriscados de tratamento de exceção.

### Parameters

| Campo | Tipo | Obrigatório | Descrição |
|---|---:|---:|---|
| `forbidEmptyCatch` | boolean | não | Proíbe `catch` vazio. |
| `forbidCatchAll` | boolean | não | Proíbe catch-all. |
| `forbidThrowEx` | boolean | não | Proíbe `throw ex;`. |
| `requireLogging` | boolean | não | Exige chamada de logging no `catch`. |

### Exemplo

```json
{
  "id": "CP7001",
  "title": "Tratamento de exceções seguro",
  "description": "Evita catches ruins.",
  "kind": "exception_handling",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "forbidEmptyCatch": true,
    "forbidCatchAll": true,
    "forbidThrowEx": true,
    "requireLogging": false
  }
}
```

---

## 8. `async_policy`

Detecta problemas comuns de código assíncrono.

### Parameters

| Campo | Tipo | Obrigatório | Descrição |
|---|---:|---:|---|
| `forbidAsyncVoid` | boolean | não | Proíbe métodos `async void`. |
| `requireCancellationToken` | boolean | não | Exige `CancellationToken` em métodos públicos async. |
| `forbidBlockingCalls` | boolean | não | Proíbe `.Result`, `.Wait()` e `GetResult()`. |

### Exemplo

```json
{
  "id": "CP8001",
  "title": "Async seguro",
  "description": "Evita padrões async ruins.",
  "kind": "async_policy",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "forbidAsyncVoid": true,
    "requireCancellationToken": true,
    "forbidBlockingCalls": true
  }
}
```

---

## Usando na Web

1. Acesse a tela de regras.
2. Crie ou edite uma regra.
3. Troque para o modo raw JSON.
4. Cole um objeto JSON válido no formato deste guia.
5. Salve.
6. Na análise de regras da solução, habilite a regra para a solução desejada e execute a análise.

## Usando arquivo JSON na CLI

A CLI aceita um arquivo `.json` com uma regra única ou um array de regras.

Regra única:

```json
{
  "id": "CP8001",
  "title": "Async seguro",
  "kind": "async_policy",
  "schemaVersion": "1.0",
  "severity": "warning",
  "enabled": true,
  "scope": {
    "projects": ["*"],
    "files": ["**/*.cs"],
    "excludeFiles": []
  },
  "parameters": {
    "forbidAsyncVoid": true
  }
}
```

Array de regras:

```json
[
  {
    "id": "CP1001",
    "title": "Evitar var",
    "kind": "syntax_presence",
    "schemaVersion": "1.0",
    "severity": "warning",
    "enabled": true,
    "scope": {
      "projects": ["*"],
      "files": ["**/*.cs"],
      "excludeFiles": []
    },
    "parameters": {
      "mode": "forbid",
      "targets": ["local_declaration"],
      "syntaxKinds": ["var"]
    }
  }
]
```

Exemplo de execução:

```bash
dotnet run --project src/CodePass.Cli -- analyze \
  --solution /caminho/para/App.sln \
  --rules /caminho/para/rules.json
```

## Erros comuns

- Incluir `language`: a autoria Web atual não usa esse campo.
- Usar `severity` fora de `info`, `warning`, `error`.
- Esquecer `schemaVersion: "1.0"`.
- Colocar número como string, por exemplo `"maxLines": "50"`; use `"maxLines": 50`.
- Colocar campo não suportado em `parameters`; a Web rejeita campos fora do schema do kind.
- Usar array com item que não é string onde o schema pede array de string.

## Fonte de verdade no código

Este guia reflete o estado atual destes arquivos:

- `src/CodePass.Web/Services/Rules/RuleCatalogService.cs`
- `src/CodePass.Web/Services/Rules/RuleDefinitionService.cs`
- `src/CodePass.Web/Services/RuleAnalysis/RoslynRuleAnalyzer.cs`
