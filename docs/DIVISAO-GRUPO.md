# Divisão do grupo — ExpenseHub

## Escopo e sequência

| Integrante | Issues | Commits funcionais planejados | Situação |
|---|---|---|---|
| Sofi | I01, I02, I03 | 3 | Implementação inicial pronta para revisão e commits locais |
| Colega 1 | I04, I05, I06 | 3 | Implementadas localmente; publicação e CI pendentes por autenticação GitHub |
| Colega 2 | I07, I08, I09 | 3 | A implementar |
| Todos | I10 | Revisão transversal e correções reais quando necessárias | Pipeline final pendente |

São nove commits de features planejados; os três de Sofi correspondem a 1/3.
I10 não é uma décima feature isolada. Merges e correções podem alterar o total.
Não criar commits vazios ou artificiais para preservar uma proporção numérica.
Cada integrante precisa de mais de um commit real no repositório final.
IA é permitida; cada aluno deve revisar, compreender, testar e defender sua parte.
Nenhum commit dos colegas foi preparado nem atribuído a eles nesta entrega.

## Colega 1 — I04, I05, I06

### Commit/PR I04 — Criar e editar rascunho

Branch: `i04-expense-draft`. Referência: Racass/checkpoint-csharpracass-expensehub#4.

Criar contratos de entrada e ExpenseService. Usar ExpenseHubDbContext e entidades
existentes. DTO recebe somente os campos editáveis; nunca proprietário, estado,
ator ou timestamps. Definir como selecionar ExpenseCategory e disponibilizar
categorias de teste; não criar usuários adicionais pelo seed.

- POST /api/expenses exige Employee; owner vem da claim NameIdentifier.
- PUT /api/expenses/{id} exige Employee e Draft próprio.
- Descrição: 10–500 caracteres após tratar espaços conforme regra documentada.
- Valor decimal: 0,01 até Int32.MaxValue.
- Data válida e não futura; relógio do servidor injetável para testes determinísticos.
- IDs e timestamps gerados no servidor, UTC para instantes.
- Estado inicial Draft. Renove Expense.Version em toda alteração.
- Prepare o histórico de criação/edição junto da alteração no mesmo SaveChanges.
- Erros de entrada 400, perfil insuficiente 403 e estado incompatível 409.
- Inexistência/invisibilidade deve seguir 404; não revelar dados de terceiros.

Mensagem sugerida: `feat(expenses): create and edit owned drafts`.

### Commit/PR I05 — Enviar e consultar

Branch: `i05-submit-query`. Referência: Racass/checkpoint-csharpracass-expensehub#5.

- POST /api/expenses/{id}/submit: Draft próprio para Submitted.
- GET /api/expenses e GET /api/expenses/{id} conforme visibilidade por perfil.
- Compor Where no IQueryable antes de ToListAsync/SingleOrDefaultAsync.
- Envio repetido retorna 409 e não grava histórico extra.
- Isolar as regras contextuais em serviço, não apenas em atributos do controller.

Mensagem sugerida: `feat(expenses): submit drafts and query visible expenses`.

### Commit/PR I06 — Matriz de acesso

Branch: `i06-ownership-access`. Referência: Racass/checkpoint-csharpracass-expensehub#6.

- Employee: próprios; Approver: Submitted; Finance: Approved/Paid; Auditor: todos.
- Roles combinadas: união da leitura, sem permitir autoaprovação/autopagamento.
- Admin sozinho não tem acesso funcional às despesas.
- Sem token: 401; sem role para operação: 403; detalhe invisível: 404.
- Oferecer validações reutilizáveis para decisões/pagamentos, respeitando estado.
- Usar o mesmo filtro de visibilidade para detalhe e histórico.
- Adicionar testes das regras implementadas, sem depender de banco/rede.

Mensagem sugerida: `feat(security): enforce expense ownership and visibility`.

## Colega 2 — I07, I08, I09

Pode preparar testes e regras puras enquanto I04–I06 são feitas. Os endpoints e a
persistência precisam integrar o serviço do colega 1; não criar um segundo domínio.

### Commit/PR I07 — Aprovar e reprovar

Branch: `i07-approve-reject`. Referência: Racass/checkpoint-csharpracass-expensehub#7.

- POST /api/expenses/{id}/approve: Submitted para Approved.
- POST /api/expenses/{id}/reject: Submitted para Rejected.
- Exigir Approver e proibir despesa própria, mesmo com roles acumuladas.
- Justificativa obrigatória de 10–500 caracteres.
- Ator do token, horário do servidor, histórico na mesma operação lógica.
- Repetição/estado inválido: 409, sem duplicação de histórico.

Mensagem sugerida: `feat(expenses): approve and reject submitted requests`.

### Commit/PR I08 — Pagamento e histórico

Branch: `i08-payment-history`. Referência: Racass/checkpoint-csharpracass-expensehub#8.

- POST /api/expenses/{id}/pay: Approved para Paid.
- Exigir Finance; proibir pagamento de despesa própria.
- PaymentRecord com ActorId autenticado e PaidAtUtc do servidor.
- GET /api/expenses/{id}/history usa a visibilidade do serviço existente.
- Registrar ação, ator, instante UTC, estados, justificativa e alterações Draft.
- Estado, PaymentRecord e ExpenseHistory devem ser gravados atomicamente.
- Renovar Version; tratar DbUpdateConcurrencyException como 409.
- O índice único de ExpenseId protege a unicidade do pagamento, mas não substitui
  a regra de estado nem o tratamento de concorrência na camada de serviço.
- Testar falha de persistência sem deixar dados/histórico divergentes.

Mensagem sugerida: `feat(expenses): record payments and auditable history`.

### Commit/PR I09 — Testes unitários das despesas

Branch: `i09-unit-tests`. Referência: Racass/checkpoint-csharpracass-expensehub#9.

- Usar MSTest no projeto existente, sem banco, rede ou serviço externo.
- Cobrir estados/transições válidas e inválidas e repetição.
- Cobrir ownership, roles combinadas, autoaprovação e autopagamento.
- Cobrir justificativas inválidas, limites de descrição/valor/data.
- Cobrir histórico, pagamento único e falha lógica de persistência.
- Se necessário, extrair interfaces pequenas de persistência e relógio; evitar
  testes de getters ou de mocks sem regra de negócio.
- Alterações defeituosas simples precisam fazer os testes relevantes falharem.
- Os oito testes de roles existentes não substituem esta suíte.

Mensagem sugerida: `test(expenses): cover transitions ownership and audit rules`.

## Contratos de integração

Namespace Domain: Expense, ExpenseCategory, ExpenseHistory, PaymentRecord,
ExpenseStatus. Contexto: ExpenseHub.Api.Persistence.ExpenseHubDbContext.

Expense.OwnerId e os ActorId referenciam IdentityUser.Id (string). Enum persistido
como texto. PaymentRecord.ExpenseId é único. Expense.Version é Guid e token de
concorrência; o serviço deve renová-lo explicitamente. A fundação apenas mapeia
entidades: ainda não aplica as validações de negócio de despesas.

Concentrar operações de despesas num serviço e coordenar alterações nele para
reduzir conflitos. Colega 2 pode começar por regras/testes e integrar depois.
Cada PR deve incluir sua própria evidência de build/teste, auto-revisão e segurança.

## I10 — Todos

Não alterar nem enfraquecer Directory.Build.props, .editorconfig, workflow,
Gitleaks ou scripts oficiais. Ler o score do artefato code-quality-report; workflow
verde não garante 100. Rodar restore, build e test no SHA final. Conferir ausência
de arquivos de banco, bin/obj, senhas, tokens e chaves. Atualizar documentação e
entregar URL pública e SHA final da main. Score final só existe após o CI do grupo.

## Formulário e prazo

O formulário deve ser preenchido com os dados reais dos três integrantes.
Nesta preparação não foram fornecidos nomes completos/RMs dos colegas nem a URL
do repositório do grupo; o formulário não foi submetido.

README e ENUNCIADO consultados informam 13/10/2026. A mensagem encaminhada cita
quarta-feira, 30 de outubro. São informações divergentes; confirmar com o professor
qual prazo vale para esta turma antes de assumir uma extensão.
