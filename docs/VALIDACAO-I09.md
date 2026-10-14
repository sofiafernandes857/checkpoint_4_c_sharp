# I09 — Testes unitários

Referência: https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/9

Suíte MSTest existente ampliada para 53 casos, sem banco, rede ou serviço externo.
ExpenseServiceTransitionTests usa o serviço real, TimeProvider fixo e um
IExpenseTransitionStore em memória. O contexto exigido pelas operações anteriores
é construído sem provider e não realiza acesso a banco nesses testes.

Cobertura adicionada:

- Aprovação/reprovação, motivo aparado e limites 10/500, estados incompatíveis.
- Autoaprovação, autoreprovação e autopagamento com roles acumuladas.
- Pagamento único, ator e horário UTC iguais no pagamento e histórico.
- Repetição sem segunda tentativa de commit ou histórico extra.
- Falha lógica de gravação e retry limpo; conflito por versão concorrente.
- Visibilidade do histórico em todos os estados, união de roles e ownership.
- Ausência de autenticação, role insuficiente e identificador inexistente.

Os testes anteriores de rascunho, acesso e envio continuam cobrindo descrição,
valor, datas, alterações Draft e as 32 combinações de roles.

## Evidências locais

Em 14/10/2026, SDK .NET 10.0.201:

- Restore concluído.
- Build sem erros e sem warnings.
- 53 testes aprovados, nenhum ignorado ou com falha.
- Mutação temporária de pagamento para manter Approved: teste
  ApprovedExpenseProducesPaymentAndHistory falhou como esperado.
- Mutação temporária do mínimo da justificativa para 1: teste
  InvalidReasonsLeaveExpenseUntouched falhou como esperado.
- Ambas as mutações foram removidas; nenhum código defeituoso faz parte dos commits.

Os testes de falha verificam a coordenação lógica do serviço. A atomicidade
relacional é fornecida pelo único SaveChanges do adaptador EF; estes testes
unitários não constituem uma simulação do motor SQLite.

O score oficial depende do artefato code-quality-report do CI. Não confundir
build/testes locais com confirmação de score 100.

## Como defender a implementação

1. Controller aplica autenticação/roles e converte erros em ProblemDetails.
2. ExpenseService coordena leitura, regras, relógio e persistência.
3. ExpenseAccessRules bloqueia ação própria mesmo com roles acumuladas.
4. ExpenseWorkflowRules valida antes de alterar e produz os registros de auditoria.
5. ExpenseTransitionStore salva estado/histórico/pagamento juntos, comparando Version.
6. Testes conferem resultados observáveis e garantem que entradas inválidas não gravem.
