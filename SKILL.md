---
name: codepass-agent-quality
description: Use os endpoints locais do CodePass para listar soluções cadastradas e executar análises de qualidade após finalizar desenvolvimento de código.
---

# codepass-agent-quality

Use esta skill quando você for um agente de IA rodando localmente e precisar medir, com fatos concretos, como ficou um projeto após alterações de código.

A skill usa dois endpoints HTTP locais do CodePass:

1. Listar soluções cadastradas e seus IDs.
2. Executar análise de regras, cobertura e quality score para uma solução cadastrada.

## Pré-requisitos

- O CodePass Web deve estar rodando localmente.
- Você deve saber a URL base do servidor local.

Exemplos comuns:

```txt
http://localhost:5000
https://localhost:5001
```

Se a URL não for informada, tente descobrir pelo comando usado para iniciar o CodePass ou pelos logs da aplicação.

## Fluxo obrigatório

Sempre siga este fluxo:

1. Liste as soluções cadastradas.
2. Escolha a solução correta pelo `displayName`, `solutionPath` ou `status`.
3. Use o `id` da solução escolhida para pedir a análise.
4. Interprete apenas os dados concretos retornados.
5. Não invente recomendações que não estejam diretamente apoiadas nos resultados.

## 1. Listar soluções cadastradas

Endpoint:

```http
GET /api/agent-quality/solutions
```

Exemplo com `curl`:

```bash
curl -s http://localhost:5000/api/agent-quality/solutions
```

Resposta esperada:

```json
[
  {
    "id": "2b89c070-58a8-4a6d-a8e4-7cf28b0d8e13",
    "displayName": "CodePass",
    "solutionPath": "/home/user/CodePass/CodePass.sln",
    "status": "Valid",
    "statusMessage": null,
    "lastValidatedAtUtc": "2026-04-30T12:00:00Z"
  }
]
```

Campos importantes:

- `id`: identificador usado para pedir análise.
- `displayName`: nome amigável da solução.
- `solutionPath`: caminho da `.sln` cadastrada.
- `status`: estado da solução cadastrada.
- `statusMessage`: detalhe quando a solução não estiver válida.
- `lastValidatedAtUtc`: última validação conhecida.

### Escolha da solução

Prefira soluções com:

```txt
status = "Valid"
```

Se houver várias soluções válidas, escolha a que tiver `solutionPath` correspondente ao repositório atual.

Exemplo de seleção manual:

```bash
curl -s http://localhost:5000/api/agent-quality/solutions | jq '.[] | {id, displayName, solutionPath, status}'
```

Se nenhuma solução corresponder ao repositório atual, reporte isso ao usuário. Não execute análise em solução errada.

## 2. Executar análise da solução

Endpoint:

```http
POST /api/agent-quality/solutions/{registeredSolutionId}/analyze
```

Substitua `{registeredSolutionId}` pelo `id` retornado no endpoint de listagem.

Body padrão:

```json
{
  "runRuleAnalysis": true,
  "runCoverageAnalysis": true
}
```

Exemplo:

```bash
curl -s -X POST http://localhost:5000/api/agent-quality/solutions/2b89c070-58a8-4a6d-a8e4-7cf28b0d8e13/analyze \
  -H "Content-Type: application/json" \
  -d '{"runRuleAnalysis":true,"runCoverageAnalysis":true}'
```

## Execução parcial

Se quiser rodar apenas regras:

```bash
curl -s -X POST http://localhost:5000/api/agent-quality/solutions/2b89c070-58a8-4a6d-a8e4-7cf28b0d8e13/analyze \
  -H "Content-Type: application/json" \
  -d '{"runRuleAnalysis":true,"runCoverageAnalysis":false}'
```

Se quiser rodar apenas cobertura:

```bash
curl -s -X POST http://localhost:5000/api/agent-quality/solutions/2b89c070-58a8-4a6d-a8e4-7cf28b0d8e13/analyze \
  -H "Content-Type: application/json" \
  -d '{"runRuleAnalysis":false,"runCoverageAnalysis":true}'
```

Não envie ambos como `false`. Isso é uma requisição inválida.

## Resposta da análise

Resposta esperada:

```json
{
  "registeredSolutionId": "2b89c070-58a8-4a6d-a8e4-7cf28b0d8e13",
  "startedAtUtc": "2026-04-30T12:10:00Z",
  "completedAtUtc": "2026-04-30T12:10:45Z",
  "ruleAnalysis": {
    "runId": "8a6cb119-408a-42c3-a60e-8742ea97c282",
    "status": "Succeeded",
    "ruleCount": 12,
    "totalViolations": 2,
    "errorCount": 1,
    "warningCount": 1,
    "infoCount": 0,
    "errorMessage": null
  },
  "coverageAnalysis": {
    "runId": "99dba488-f11b-44a7-9d42-65458aa4b5bb",
    "status": "Succeeded",
    "projectCount": 2,
    "classCount": 34,
    "coveredLineCount": 820,
    "totalLineCount": 1000,
    "lineCoveragePercent": 82.0,
    "coveredBranchCount": 180,
    "totalBranchCount": 240,
    "branchCoveragePercent": 75.0,
    "errorMessage": null
  },
  "qualityScore": {
    "registeredSolutionId": "2b89c070-58a8-4a6d-a8e4-7cf28b0d8e13",
    "score": 84.5,
    "status": "Pass",
    "ruleContribution": {
      "maxPoints": 50,
      "earnedPoints": 42,
      "evidenceStatus": "Succeeded",
      "errorCount": 1,
      "warningCount": 1,
      "infoCount": 0,
      "totalViolations": 2,
      "summary": "2 violations",
      "blockingReasons": []
    },
    "coverageContribution": {
      "maxPoints": 50,
      "earnedPoints": 42.5,
      "evidenceStatus": "Succeeded",
      "lineCoveragePercent": 82.0,
      "coveredLineCount": 820,
      "totalLineCount": 1000,
      "summary": "82% line coverage",
      "blockingReasons": []
    },
    "blockingReasons": []
  }
}
```

## Como interpretar

Interprete somente fatos retornados pela API.

### Regras

Use `ruleAnalysis` para reportar:

- `status`
- `ruleCount`
- `totalViolations`
- `errorCount`
- `warningCount`
- `infoCount`
- `errorMessage`, se houver

Exemplo de resumo factual:

```txt
Análise de regras: Succeeded. 12 regras executadas. 2 violações encontradas: 1 error, 1 warning, 0 info.
```

### Cobertura

Use `coverageAnalysis` para reportar:

- `status`
- `projectCount`
- `classCount`
- `lineCoveragePercent`
- `coveredLineCount`
- `totalLineCount`
- `branchCoveragePercent`
- `coveredBranchCount`
- `totalBranchCount`
- `errorMessage`, se houver

Exemplo de resumo factual:

```txt
Cobertura: Succeeded. 82.0% de linhas cobertas (820/1000) e 75.0% de branches cobertos (180/240).
```

### Quality score

Use `qualityScore` para reportar:

- `score`
- `status`
- `blockingReasons`
- contribuições de regras e cobertura

Exemplo:

```txt
Quality score: 84.5, status Pass.
```

Se `status` for `Fail`, reporte o fato e os `blockingReasons` retornados.

## O que não fazer

Não gere recomendações próprias.

Não diga:

```txt
Você deve corrigir X.
Você precisa adicionar testes em Y.
Priorize Z.
```

A menos que isso seja uma conclusão direta e explicitamente solicitada pelo usuário.

Prefira:

```txt
A análise retornou 1 violação Error.
A cobertura de branches retornou 75.0%.
O quality score retornou status Fail com os seguintes blockingReasons: [...].
```

## Tratamento de erro

### 404

Significa que o `registeredSolutionId` não existe.

Ação:

1. Liste novamente as soluções.
2. Escolha um ID válido.
3. Se a solução não existir, informe o usuário.

### 400

Pode ocorrer se a requisição for inválida, por exemplo:

```json
{
  "runRuleAnalysis": false,
  "runCoverageAnalysis": false
}
```

Ação:

- Envie pelo menos uma análise como `true`.

### Falha interna de análise

Se `ruleAnalysis.status` ou `coverageAnalysis.status` vier como falha, reporte:

- `status`
- `errorMessage`

Não oculte falhas.

## Resumo final recomendado

Ao finalizar, responda em formato curto:

```txt
Solução analisada: <displayName> (<id>)
Regras: <status>, <ruleCount> regras, <totalViolations> violações (<errorCount> error, <warningCount> warning, <infoCount> info)
Cobertura: <status>, linhas <lineCoveragePercent>% (<coveredLineCount>/<totalLineCount>), branches <branchCoveragePercent>% (<coveredBranchCount>/<totalBranchCount>)
Quality score: <score>, status <status>
Blocking reasons: <lista ou "nenhum">
```
