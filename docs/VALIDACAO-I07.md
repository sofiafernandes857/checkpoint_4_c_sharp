# I07 — Aprovação e reprovação

Referência: https://github.com/Racass/checkpoint-csharpracass-expensehub/issues/7

`POST /api/expenses/{id}/approve` não recebe corpo. `POST /api/expenses/{id}/reject`
recebe `{"justification":"Despesa fora da política"}`. A justificativa é aparada
com `Trim` e deve ter 10–500 caracteres; campos desconhecidos são rejeitados.

Ambas exigem Approver, despesa Submitted e ator diferente do proprietário.
Roles acumuladas não removem essa proibição. Ator vem das claims; horário é UTC
do TimeProvider. A transição renova Version e salva seu histórico no mesmo
SaveChanges. Repetição retorna 409. Sem autenticação: 401; perfil insuficiente
ou decisão própria: 403; identificador inexistente: 404; motivo inválido: 400.

Os testes ExpenseDecisionTests cobrem transições, auditoria, limites do motivo,
estados incompatíveis e ownership com todas as roles. Execute:

```powershell
dotnet build ./sources/ExpenseHub.slnx --no-restore
dotnet test ./sources/ExpenseHub.slnx --no-restore
```

CI e score remoto devem ser conferidos na PR antes do merge.
