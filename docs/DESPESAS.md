# Despesas — I04 a I09

## Aprovação, reprovação, pagamento e histórico — I07/I08

As operações abaixo já estão integradas ao ExpenseService e às regras da I06:

| Rota | Perfil e regra | Corpo |
|---|---|---|
| POST /api/expenses/{id}/approve | Approver, não proprietário, Submitted → Approved | Sem corpo |
| POST /api/expenses/{id}/reject | Approver, não proprietário, Submitted → Rejected | `{"justification":"Despesa fora da política"}` |
| POST /api/expenses/{id}/pay | Finance, não proprietário, Approved → Paid | Sem corpo |
| GET /api/expenses/{id}/history | Mesma visibilidade de lista/detalhe | Sem corpo |

Justificativa de reprovação: 10–500 caracteres após Trim. Aprovação não exige
justificativa, conforme a issue oficial I07. Ator, IDs, versão, estados e instantes
UTC são definidos pelo servidor. Decisões e pagamento retornam 200 com a despesa.
Repetição/estado incompatível/conflito concorrente retornam 409, sem histórico extra.
O histórico invisível retorna 404; Admin sozinho recebe 403.

O adaptador ExpenseTransitionStore grava a despesa, o histórico e, quando houver,
PaymentRecord em um único SaveChanges transacional. Sua interface permite testes
unitários de falha sem banco. O filtro de histórico usa ExpenseAccessRules.Visibility
no SQL. Os estados Rejected/Paid permanecem finais.

Evidências: [I07](VALIDACAO-I07.md), [I08](VALIDACAO-I08.md) e [I09](VALIDACAO-I09.md).
As seções abaixo registram a implementação original de I04–I06; as referências
a funcionalidades futuras nessas seções foram atendidas pela integração acima.

## Contratos

As rotas `POST /api/expenses` e `PUT /api/expenses/{id}` exigem a role
`Employee`. O cadastro público não atribui essa role: o Admin precisa concedê-la,
e o usuário deve fazer novo login.

O corpo recebe somente `description`, `amount`, `expenseDate` e `categoryId`:

```json
{
  "description": "Almoço de trabalho",
  "amount": 25.50,
  "expenseDate": "2026-10-14",
  "categoryId": "10000000-0000-0000-0000-000000000001"
}
```

Campos desconhecidos são rejeitados, inclusive proprietário, identificador,
estado, versão e horários. O proprietário é obtido da claim `NameIdentifier`.
Criação retorna 201 com a despesa e o cabeçalho Location; edição retorna 200.

A descrição tem seus espaços nas extremidades removidos antes da validação de
10–500 caracteres. A validação declarativa também limita o texto bruto a 500
caracteres. Valor: decimal entre 0,01 e 2.147.483.647, inclusive. A data não pode
ser ausente, o valor mínimo de DateOnly ou futura: hoje é calculado em UTC usando
TimeProvider, registrado por injeção de dependência. A categoria precisa existir.

Edição exige proprietário e estado Draft. Despesa inexistente ou de outro usuário
retorna 404; estado diferente de Draft retorna 409. Ausência de autenticação retorna
401, falta de Employee retorna 403 e entrada inválida retorna 400, com ProblemDetails.

## Categorias

`GET /api/expense-categories`, protegido por Employee, retorna as categorias
disponíveis. A inicialização adiciona categorias ausentes dentro da transação de
seed, sem criar usuários adicionais ou substituir categorias existentes:

| ID | Nome |
|---|---|
| `10000000-0000-0000-0000-000000000001` | Alimentação |
| `10000000-0000-0000-0000-000000000002` | Transporte |
| `10000000-0000-0000-0000-000000000003` | Hospedagem |

Se o banco já tiver uma categoria com o mesmo nome e outro ID, o seed preserva
essa categoria. Consulte a rota para obter os IDs efetivamente disponíveis.

## Histórico e integração

ExpenseService coordena persistência e autorização. ExpenseDraftRules concentra
validação e mutações testáveis sem banco/rede. Despesa e ExpenseHistory são
gravados no mesmo SaveChangesAsync, transacional no provider relacional.

Criação registra `Created`, estado anterior nulo e posterior Draft. Edição registra
`Edited`, estados Draft e um JSON em Changes com valores `previous`/`current` de
cada campo alterado. Uma edição com campos idênticos registra Changes vazio `{}`.
IDs, atores e instantes são definidos no servidor; os instantes são UTC.

Cada edição renova Version. DbUpdateConcurrencyException retorna 409 e limpa o
rastreamento das alterações que não foram persistidas. O token é usado para
concorrência entre operações no servidor; esta versão não implementa If-Match
para detectar formulários antigos enviados posteriormente por clientes.

O endpoint de detalhe indicado em Location está disponível na I05. As operações
de aprovação, pagamento e consulta de histórico devem integrar o serviço existente
nas próximas issues. Nenhuma alteração de esquema foi necessária na I04/I05.

## Envio e consultas — I05

`POST /api/expenses/{id}/submit` exige Employee e Draft próprio. Retorna 200 com
a despesa em Submitted, renova Version e registra histórico `Submitted` no mesmo
SaveChangesAsync. Repetições retornam 409 sem histórico extra. Despesa de outro
usuário ou inexistente retorna 404. O endpoint não recebe campos de transição:
ator, estado e instante são sempre derivados do servidor.

`GET /api/expenses` e `GET /api/expenses/{id}` exigem uma role funcional:

- Employee: despesas próprias, em qualquer estado.
- Approver: Submitted.
- Finance: Approved/Paid.
- Auditor: todas as despesas.
- Admin sozinho e usuário sem roles: 403.

Roles acumuladas dão a união da leitura. ExpenseAccessRules.Visibility retorna
uma expressão aplicada no IQueryable antes da materialização. Detalhes invisíveis
e inexistentes retornam 404. A listagem ordena por data da despesa decrescente e
ID, operações traduzidas pelo SQLite. Não há paginação nesta etapa.

O filtro é reutilizável na futura consulta de histórico. Consulta não autoriza
decisões ou pagamento: o colega da I07/I08 deve aplicar também os bloqueios de
ownership e estado.

## Integração da autorização — I06

ExpenseAccessRules concentra a matriz contextual:

- ActorId/RequireRole exigem identidade autenticada e identificador válido.
- RequireOwnedDraft valida ownership antes do estado; falha de ownership retorna
  404 e estado incompatível retorna 409. Edição e envio usam a mesma regra.
- Visibility é o filtro compartilhado de listagem/detalhe. O endpoint de histórico
  da I08 deve localizar a despesa com esse filtro antes de carregar o histórico.
- RequireDecision exige Approver, ator diferente do proprietário e Submitted.
  Deve ser chamada antes de aprovação e reprovação na I07.
- RequirePayment exige Finance, ator diferente do proprietário e Approved.
  Deve ser chamada antes do pagamento na I08.

As duas últimas regras retornam o ID autenticado e não alteram a despesa. Sem
autenticação/ID válido retornam 401, sem role ou em despesa própria retornam 403
e estado incompatível retorna 409. A proibição de ação própria é preservada com
qualquer combinação de roles. Os futuros endpoints ainda precisam localizar o
recurso, validar os dados da ação, gerar histórico e persistir atomicamente.

Os testes unitários verificam as 32 combinações das cinco roles contra todos os
estados e proprietários, além de bloqueios de ação própria, estado e autenticação.
As rotas de aprovação, reprovação, pagamento e histórico não foram implementadas
nesta parte; pertencem ao outro integrante.

## Validação

```powershell
dotnet build ./sources/ExpenseHub.slnx
dotnet test ./sources/ExpenseHub.slnx
```

ExpenseDraftRulesTests cobre campos gerados no servidor, limites, datas, ownership,
estados não editáveis, versão, alterações auditáveis e rejeição de campos protegidos.
Os testes não dependem de banco, rede ou relógio real.
